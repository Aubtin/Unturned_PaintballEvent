using Rocket.Unturned.Chat;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rocket.API;
using Rocket.Unturned.Player;

namespace datathegenius.paintballevent
{
    public class CommandPaintball : IRocketCommand
    {
        public static Boolean started = false;
        public static int activePlayers = 0;
        public static Boolean eventActive = false;

        public static Vector3 position1;
        public static Vector3 position2;

        public static List<string> usedStorage = new List<string>();

        public List<string> Aliases
        {
            get
            {
                return new List<string>();
            }
        }

        public AllowedCaller AllowedCaller
        {
            get
            {
                return AllowedCaller.Player;
            }
        }

        public string Help
        {
            get
            {
                return "Allows player functions for paintball.";
            }
        }

        public string Name
        {
            get
            {
                return "paintball";
            }
        }

        public List<string> Permissions
        {
            get
            {
                return new List<string>() { "paintballevent.players" };
            }
        }

        public string Syntax
        {
            get
            {
                return "<command>";
            }
        }

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer pCaller = (UnturnedPlayer)caller;

            //Claim reward from paintball.
            if (command[0] == "reward")
            {
                if(PaintballManager.paintballOn)
                {
                    UnturnedChat.Say(caller, "Must wait until paintball ends.", Color.red);
                    return;
                }

                int winCount;
                Boolean exists = PaintballManager.playerWins.TryGetValue(pCaller.CSteamID, out winCount);

                if(exists == false)
                {
                    UnturnedChat.Say(caller, "Sorry, you either claimed your reward already or did not win anything.", Color.red);
                    return;
                }

                //Give reward
                for (int x = 0; x <= winCount; x++)
                {
                    string prize = PaintballManager.paintballRewards[x];
                    int totalPrizes = prize.ToCharArray().Count(c => c == ',');

                    Boolean addItemToInventory = true;
                    for (int y = 0; y <= totalPrizes; y++)
                    {
                        addItemToInventory = pCaller.Inventory.tryAddItem(new Item((ushort)Convert.ToInt32(prize.Split(',')[y]), true), true);

                        if(addItemToInventory == false)
                        {
                            ItemManager.dropItem(new Item((ushort)Convert.ToInt32(prize.Split(',')[y]), true), pCaller.Position, true, true, true);
                            UnturnedChat.Say(caller, "A reward item was dropped below you.", Color.cyan);
                        }
                    }
                }
                PaintballManager.playerWins.Remove(pCaller.CSteamID);
                UnturnedChat.Say(pCaller, "Your rewards for winning " + winCount + " times has been given.", Color.cyan);
                return;
            }

            if (PaintballManager.paintballOn)
            {
                //Join
                if (command[0] == "join")
                {
                    join(pCaller);
                }

                //Leave
                if (command[0] == "leave")
                {
                    leave(pCaller);

                    //Since they left early, let them know they'll get their reward after.
                    UnturnedChat.Say(pCaller, "You'll get your reward once the event is over!", Color.green);
                }

                //Win count.
                if(command[0] == "wins")
                {
                    int winCount;
                    Boolean exists = PaintballManager.playerWins.TryGetValue(pCaller.CSteamID, out winCount);

                    if(exists == false)
                    {
                        UnturnedChat.Say(caller, "You haven't played in paintball to have won any games.", Color.red);
                        return;
                    }

                    UnturnedChat.Say(caller, "You have won " + winCount +" times.", Color.green);
                }
            }
            else
            {
                UnturnedChat.Say(caller, "Cannot use this command, paintball is not on.", Color.red);
            }
        }

        public void join(UnturnedPlayer tempPlayer)
        {
            //Check for duplicate player and group.
            int checkResult = PlayerManager.duplicatePlayerGroupCheck(tempPlayer);
            if (checkResult == 0 || checkResult == 1)
                return;

            //If they're driving, have them get out first.
            if (tempPlayer.Stance == EPlayerStance.DRIVING || tempPlayer.Stance == EPlayerStance.SITTING)
            {
                UnturnedChat.Say(tempPlayer, "Please exit the vehicle then try to join paintball.", Color.red);
                return;    
            }

            //Player passed check, create PlayerManager object.
            PlayerManager playerObj = new PlayerManager(tempPlayer);

            //Add player to joined list.
            PaintballManager.joinedPlayers.Add(playerObj);

            //Set their permissions.
            Rocket.Core.R.Permissions.AddPlayerToGroup("EventGroup", tempPlayer);
            Rocket.Core.R.Permissions.RemovePlayerFromGroup("Guest", tempPlayer);

            //Save inventory and clear player.
            playerObj.saveAndClearInventory();

            //Add them to reward list if they aren't there already.
            if (!PaintballManager.playerWins.ContainsKey(tempPlayer.CSteamID))
                PaintballManager.playerWins.Add(tempPlayer.CSteamID, 0);

            //Notify player of joining.
            UnturnedChat.Say(tempPlayer, "You have joined paintball!", Color.green);

            if (PaintballManager.gameRunning)
            {
                tempPlayer.Damage(255, new Vector3(0, 0, 0), EDeathCause.PUNCH, ELimb.SKULL, tempPlayer.CSteamID);
                return;
            }

            //Send player to waiting room (waiting) and activate god for waitroom.
            tempPlayer.Teleport(PaintballManager.waitingPosition, PaintballManager.waitingRotation);
            tempPlayer.GodMode = true;

            return;
        }

        public void leave(UnturnedPlayer tempPlayer)
        {
            //Find the PlayerManager object.
            PlayerManager player = PlayerManager.findPlayerManagerObject(tempPlayer);

            if(player == null)
            {
                UnturnedChat.Say(tempPlayer, "Can't leave paintball, you aren't joined.", Color.red);
                return;
            }

            //Remove player from joined list.
            PaintballManager.joinedPlayers.Remove(player);

            //Set their permissions.
            Rocket.Core.R.Permissions.RemovePlayerFromGroup("EventGroup", tempPlayer);
            Rocket.Core.R.Permissions.AddPlayerToGroup("Guest", tempPlayer);

            //Clear their inventory for good measure.
            PlayerManager.clearInventory(tempPlayer);

            //Give them their items back.
            player.updatePlayer(tempPlayer);
            player.returnInventory();

            //Teleport player to starting position.
            tempPlayer.Teleport(player.getLocation(), player.getRotation());
            tempPlayer.GodMode = false;

            //Let the player know.
            UnturnedChat.Say(tempPlayer, "You have left paintball.", Color.green);
        }

        public void leaveByDisconnect(UnturnedPlayer tempPlayer)
        {
            var player = PaintballManager.leftEarly.FirstOrDefault(item => item.getSteamID() == tempPlayer.CSteamID);

            //Remove player from joined list.
            PaintballManager.joinedPlayers.Remove(player);

            //Set their permissions.
            Rocket.Core.R.Permissions.RemovePlayerFromGroup("EventGroup", tempPlayer);
            Rocket.Core.R.Permissions.AddPlayerToGroup("Guest", tempPlayer);

            //Teleport player to starting position.
            tempPlayer.Teleport(player.getLocation(), player.getRotation());
            tempPlayer.GodMode = false;

            
            //Give them their items back.
            player.updatePlayer(tempPlayer);
            PlayerManager.clearInventory(tempPlayer);
            player.returnInventory();
            PaintballManager.leftEarly.Remove(player);

            //Let the player know.
            UnturnedChat.Say(tempPlayer, "You left the server while you were joined in paintball.", Color.green);
        }
    }
}
