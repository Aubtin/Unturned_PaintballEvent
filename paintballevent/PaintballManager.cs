using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Rocket.Unturned.Player;
using Steamworks;
using Rocket.Unturned.Enumerations;
using SDG.Unturned;
using Rocket.Unturned.Chat;
using Rocket.Core.Logging;

namespace datathegenius.paintballevent
{
    public class PaintballManager : RocketPlugin<PaintballConfiguration>
    {
        public static PaintballManager Instance;

        public static Boolean paintballOn = false;
        public static Boolean gameRunning = false;

        public static List<PlayerManager> joinedPlayers = new List<PlayerManager>() { };

        public static List<UnturnedPlayer> deadPlayerForTransport = new List<UnturnedPlayer>() { };
        
        //Left early
        public static List<PlayerManager> leftEarly = new List<PlayerManager>();
        public List<UnturnedPlayer> processEarlyLeaver = new List<UnturnedPlayer>();

        //Win count.
        public static Dictionary<int, string> paintballRewards;
        public static Dictionary<CSteamID, int> playerWins = new Dictionary<CSteamID, int>();
        
        //Coordinates
        public static Vector3 waitingPosition;
        public static float waitingRotation;

        //Arena coordinates
        public static Vector3 arenaPosition1;
        public static Vector3 arenaPosition2;
        public static Vector3 arenaPosition;
        public static float arenaRotation;
        public static int arenaMinX;
        public static int arenaMaxX;
        public static int arenaMinZ;
        public static int arenaMaxZ;
        public static int arenaY;
        public static int randomSizeX;
        public static int randomSizeZ;

        //Time holders
        DateTime playerReconnected;

        public static int alive;

        protected override void Load()
        {
            Instance = this;

            U.Events.OnPlayerDisconnected += OnPlayerDisconnected;
            U.Events.OnPlayerConnected += OnPlayerConnected;
            UnturnedPlayerEvents.OnPlayerRevive += OnPlayerRevive;
            UnturnedPlayerEvents.OnPlayerDead += OnPlayerDead;

            //Player prizes integrated in code... Change later.
            paintballRewards = new Dictionary<int, string>() { { 0, "253,334" },
                                                               { 1, "307,308,309,310" },
                                                               { 2, "1337,1339,1339,1339,1339,1339,1339,1339,1339,1339,1339" },
                                                               { 3, "235,236,237,238" },
                                                               { 4, "116,363,17,17" },
                                                               { 5, "122,125" },
                                                               { 6, "132,126" },
                                                               { 7, "297,18" },
                                                               { 8, "1377,1375,1362" },
                                                               { 9, "1382,1384,1384" },
                                                               { 10, "1364, 1365" } };

            CommandPaintballOperations.arenaPosition1Temp = new Vector3(-9999, -9999, -9999);
            CommandPaintballOperations.arenaPosition2Temp = new Vector3(-9999, -9999, -9999);
        }

        protected override void Unload()
        {
            U.Events.OnPlayerDisconnected -= OnPlayerDisconnected;
            U.Events.OnPlayerConnected -= OnPlayerConnected;
            UnturnedPlayerEvents.OnPlayerRevive -= OnPlayerRevive;
            UnturnedPlayerEvents.OnPlayerDead -= OnPlayerDead;
        }

        void FixedUpdate()
        {
            delayRecoverPlayerLeft();
            transportDead();
            announcementManager();
        }

        private void OnPlayerConnected(UnturnedPlayer player)
        {
            var playerLeftEarly = PaintballManager.leftEarly.FirstOrDefault(item => item.getSteamID() == player.CSteamID);

            if(playerLeftEarly != null)
            {
                playerReconnected = DateTime.Now;

                processEarlyLeaver.Add(player);
                delayRecoverPlayerLeft();
            }
        }

        private void OnPlayerDisconnected(UnturnedPlayer player)
        {
            if (PaintballManager.paintballOn)
            {
                var playerLeftEarly = PaintballManager.joinedPlayers.FirstOrDefault(item => item.getSteamID() == player.CSteamID);

                if (playerLeftEarly != null)
                {
                    leftEarly.Add(playerLeftEarly);
                    joinedPlayers.Remove(playerLeftEarly);
                    PlayerManager.clearInventory(player);
                }

                if(PaintballManager.gameRunning)
                    checkWin();
            }
        }

        private void OnPlayerRevive(UnturnedPlayer player, Vector3 position, byte angle)
        {
            if (PaintballManager.paintballOn)
            {
                PlayerManager currentPlayer = PlayerManager.findPlayerManagerObject(player);

                if (currentPlayer != null)
                {
                    currentPlayer.setRevived(true);
                    deadPlayerForTransport.Add(player);
                    player.GodMode = true;
                }
            }
        }

        private void OnPlayerDead(UnturnedPlayer player, Vector3 position)
        {
            if(PaintballManager.paintballOn)
            {
                PlayerManager currentPlayer = PlayerManager.findPlayerManagerObject(player);

                if(currentPlayer != null)
                {
                    PlayerManager.clearInventory(player);
                    currentPlayer.setDead(true);
                    currentPlayer.setRevived(false);
                    checkWin();
                }
            }
        }

        //Delay item give back when player rejoins.
        private void delayRecoverPlayerLeft()
        {
            if (processEarlyLeaver.Count == 1 && processEarlyLeaver[0] != null)
            {
                if ((DateTime.Now - this.playerReconnected).TotalSeconds > .5)
                {
                    playerReconnected = DateTime.Now;

                    CommandPaintball paintball = new CommandPaintball();
                    UnturnedPlayer tempPlayer = processEarlyLeaver[0];

                    paintball.leaveByDisconnect(tempPlayer);

                    processEarlyLeaver.Remove(processEarlyLeaver[0]);
                }
            }
        }

        //Delay transport on death.
        DateTime lastCalledDead = DateTime.Now;
        private void transportDead()
        {
            if(PaintballManager.paintballOn)
            {
                lastCalledDead = DateTime.Now;

                if (deadPlayerForTransport.Count == 1 && deadPlayerForTransport[0] != null)
                {
                    deadPlayerForTransport[0].Teleport(PaintballManager.waitingPosition, PaintballManager.waitingRotation);
                    deadPlayerForTransport.Remove(deadPlayerForTransport[0]);
                }
            }
        }

        //Count alive.
        public int aliveCount()
        {
            int deathCount = 0;

            for (int x = 0; x < PaintballManager.joinedPlayers.Count(); x++)
            {
                if (PaintballManager.joinedPlayers[x].getDead())
                    deathCount++;
            }

            return (PaintballManager.joinedPlayers.Count - deathCount);
        }

        //Check for win.
        public void checkWin()
        {
            //Count and deal with alive people.
            int tempTotalAlive = aliveCount();
            Logger.Log("Paintball Current Alive: " + tempTotalAlive);
            alive = tempTotalAlive;

            if (tempTotalAlive == 1)
            {
                var winningPlayer = PaintballManager.joinedPlayers.FirstOrDefault(item => item.getDead() == false);

                UnturnedPlayer playerWinner = winningPlayer.getPlayer();

                //Increase wins.
                playerWins[playerWinner.CSteamID]++;

                PlayerManager.clearInventory(playerWinner);
                playerWinner.Damage(255, new Vector3(0, 0, 0), EDeathCause.PUNCH, ELimb.SKULL, playerWinner.CSteamID);

                UnturnedChat.Say("The paintball winner this round is: " + playerWinner.CharacterName + "!", Color.cyan);

                //Called when the current game naturally ends.
                PaintballManager.gameRunning = false;
            }
        }

        DateTime lastCalledTimer = DateTime.Now;

        private void announcementManager()
        {
            if (((DateTime.Now - this.lastCalledTimer).TotalSeconds > 30))
            {
                lastCalledTimer = DateTime.Now;

                if (PaintballManager.paintballOn)
                {
                    UnturnedChat.Say("Join paintball! Do '/paintball join' to join. Your inventory and experience is saved!", Color.cyan);
                }

                if (!PaintballManager.paintballOn && PaintballManager.playerWins.Count() > 0)
                {
                    foreach (SteamPlayer plr in Provider.Players)
                    {
                        //So let's convert each SteamPlayer into an UnturnedPlayer
                        UnturnedPlayer unturnedPlayer = UnturnedPlayer.FromSteamPlayer(plr);

                        if (PaintballManager.playerWins.ContainsKey(unturnedPlayer.CSteamID))
                            UnturnedChat.Say(unturnedPlayer, "You have a prize from paintball, claim it with '/paintball reward'.", Color.cyan);
                    }
                }
            }

            
        }
    }
}
