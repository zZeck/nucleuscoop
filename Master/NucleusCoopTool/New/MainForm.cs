﻿using Nucleus.Gaming;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Nucleus.Coop
{
    public partial class MainForm : BaseForm
    {
        private int currentStepIndex;
        private bool formClosing;
        private IGameHandler handler;
        private bool expanded = false;

        private Size expandedSize = new Size(1070, 740);
        private Size startSize = new Size(275, 740);

        private GameManager gameManager;
        private Dictionary<UserGameInfo, GameControl> controls;

        private SearchDisksForm form;

        private GameControl currentControl;
        private UserGameInfo currentGameInfo;
        private IGameInfo currentGame;
        private GameProfile currentProfile;
        private bool noGamesPresent;
        private List<UserInputControl> stepsList;
        private UserInputControl currentStep;

        private PlayerCountControl countControl;
        private PositionsControl positionsControl;
        private PlayerOptionsControl optionsControl;

        public MainForm()
        {
            InitializeComponent();
            Size = startSize;

            controls = new Dictionary<UserGameInfo, GameControl>();
            gameManager = new GameManager();

            countControl = new PlayerCountControl();
            positionsControl = new PositionsControl();
            optionsControl = new PlayerOptionsControl();

            countControl.OnCanPlay += StepCanPlay;
            positionsControl.OnCanPlay += StepCanPlay;
            optionsControl.OnCanPlay += StepCanPlay;
        }

        private void Expand()
        {
            Size = expandedSize;
            expanded = true;
        }

        public void RefreshGames()
        {
            foreach (var con in controls)
            {
                if (con.Value != null)
                {
                    con.Value.Dispose();
                }
            }

            controls.Clear();
            this.list_Games.Controls.Clear();

            List<UserGameInfo> games = gameManager.User.Games;
            for (int i = 0; i < games.Count; i++)
            {
                UserGameInfo game = games[i];
                NewUserGame(game);
            }

            if (games.Count == 0)
            {
                noGamesPresent = true;
                GameControl con = new GameControl();
                con.Width = list_Games.Width;
                con.Text = "No games";
                this.list_Games.Controls.Add(con);
            }
        }

        public void NewUserGame(UserGameInfo game)
        {
            if (noGamesPresent)
            {
                this.list_Games.Controls.Clear();
                noGamesPresent = false;
            }

            if (game.Game == null)
            {
                return;
            }

            GameControl con = new GameControl();
            con.Game = game;
            con.Width = list_Games.Width;

            controls.Add(game, con);

            con.Text = game.Game.GameName;
            ThreadPool.QueueUserWorkItem(GetIcon, game);

            this.list_Games.Controls.Add(con);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RefreshGames();
        }

        private void GetIcon(object state)
        {
            UserGameInfo game = (UserGameInfo)state;
            Icon icon = Shell32.GetIcon(game.ExePath, false);

            Bitmap bmp = icon.ToBitmap();
            icon.Dispose();
            game.Icon = bmp;

            GameControl control = controls[game];
            control.Image = game.Icon;
        }

        private void list_Games_SelectedChanged(object arg1, Control arg2)
        {
            currentControl = (GameControl)arg1;
            currentGameInfo = currentControl.Game;
            if (currentGameInfo == null)
            {
                return;
            }

            Expand();

            panelGameName.Visible = true;
            label_StepTitle.Visible = true;
            StepPanel.Visible = true;
            btnBack.Visible = true;

            currentGame = currentGameInfo.Game;

            btn_Play.Enabled = false;

            if (!currentGame.SupportsPositioning &&
                currentGame.Options.Length == 0)
            {
                // can play
                btn_Play.Enabled = true;

                // remove the current step if there's one
                KillCurrentStep();

                btnBack.Visible = false;
            }

            stepsList = new List<UserInputControl>();
            if (currentGame.SupportsPositioning)
            {
                stepsList.Add(countControl);
                stepsList.Add(positionsControl);
            }
            if (currentGame.Options.Length != 0)
            {
                stepsList.Add(optionsControl);
            }

            currentProfile = new GameProfile();
            currentProfile.InitializeDefault(currentGame);

            this.label_GameTitle.Text = currentGame.GameName;
            this.pic_Game.Image = currentGameInfo.Icon;

            GoToStep(0);
        }

        private void EnablePlay()
        {
            btn_Play.Enabled = true;
        }

        private void StepCanPlay(UserControl obj)
        {
            if (currentStepIndex + 1 > stepsList.Count - 1)
            {
                EnablePlay();
                return;
            }

            GoToStep(currentStepIndex + 1);
        }


        private void KillCurrentStep()
        {
            this.StepPanel.Controls.Clear();
        }

        private void GoToStep(int step)
        {
            btnBack.Enabled = step > 0;

            if (step >= stepsList.Count - 1)
            {
                if (step >= stepsList.Count)
                {
                    return;
                }
            }

            KillCurrentStep();

            currentStepIndex = step;
            currentStep = stepsList[step];
            currentStep.Size = StepPanel.Size;

            if (currentStep.Profile != currentProfile)// dont reinitialize, user is coming back
            {
                currentStep.Initialize(currentGameInfo, currentProfile);
            }

            StepPanel.Controls.Add(currentStep);
            currentStep.Size = StepPanel.Size; // for some reason this line must exist or the PositionsControlg et messed up

            label_StepTitle.Text = currentStep.Title;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            formClosing = true;
        }

        private void btn_Play_Click(object sender, EventArgs e)
        {
            if (handler != null)
            {
                return;
            }

            handler = gameManager.MakeHandler(currentGame);
            handler.Initialize(currentGameInfo, currentProfile);
            handler.Ended += handler_Ended;

            if (handler.TimerInterval > 0)
            {
                Thread t = new Thread(UpdateGameManager);
                t.Start();
            }

            gameManager.Play(handler);
        }

        private void handler_Ended()
        {
            handler = null;
        }

        private void UpdateGameManager(object state)
        {
            for (;;)
            {
                try
                {
                    if (gameManager == null || formClosing || handler == null)
                    {
                        break;
                    }

                    handler.Update(handler.TimerInterval);
                    Thread.Sleep(handler.TimerInterval);
                }
                catch
                {
                }
            }
        }

        private void arrow_Back_Click(object sender, EventArgs e)
        {
            currentStepIndex--;
            if (currentStepIndex < 0)
            {
                currentStepIndex = 0;
                return;
            }
            GoToStep(currentStepIndex);
        }

        private void arrow_Next_Click(object sender, EventArgs e)
        {
            currentStepIndex = Math.Min(currentStepIndex++, stepsList.Count - 1);
            GoToStep(currentStepIndex);
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog open = new OpenFileDialog())
            {
                open.Filter = "Game Executable Files|*.exe";
                if (open.ShowDialog() == DialogResult.OK)
                {
                    string path = open.FileName;

                    UserGameInfo game = gameManager.TryAddGame(path);

                    if (game == null)
                    {
                        MessageBox.Show("Game not added/unknown");
                    }
                    else
                    {
                        MessageBox.Show("Game recognized as " + game.Game.GameName);
                        RefreshGames();
                    }
                }
            }
        }

        private void btnAutoSearch_Click(object sender, EventArgs e)
        {
            if (form != null)
            {
                return;
            }

            form = new SearchDisksForm(this);
            form.FormClosed += Form_FormClosed;
            form.Show();
            SetUpForm(form);
        }

        private void Form_FormClosed(object sender, FormClosedEventArgs e)
        {
            form = null;
        }
    }
}