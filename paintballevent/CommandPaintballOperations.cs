using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Items;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace datathegenius.paintballevent
{
    public class CommandPaintballOperations : IRocketCommand
    {
        public static Vector3 arenaPosition1Temp;
        public static Vector3 arenaPosition2Temp;
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
                return AllowedCaller.Both;
            }
        }

        public string Help
        {
            get
            {
                return "Deals with paintball operations.";
            }
        }

        public string Name
        {
            get
            {
                return "paintballops";
            }
        }

        public List<string> Permissions
        {
            get
            {
                return new List<string>() { "paintballevent.admins" };
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
            //On
            if (command[0] == "on")
            {
                //Check if already on.
                if(PaintballManager.paintballOn)
                {
                    UnturnedChat.Say(caller, "Paintball is already on.", Color.red);
                    return;
                }

                //Reset the player list, winner list, and set paintballOn to true.
                PaintballManager.joinedPlayers = new List<PlayerManager>() { };
                PaintballManager.playerWins = new Dictionary<Steamworks.CSteamID, int>();
                PaintballManager.paintballOn = true;

                UnturnedChat.Say(caller, "Paintball has been turned on successfully.", Color.green);
                return;
            }

            //Off
            if(command[0] == "off")
            {
                //Check if already off.
                if(!PaintballManager.paintballOn)
                {
                    UnturnedChat.Say(caller, "Paintball is already off.", Color.red);
                    return;
                }

                //Remove all the remaining players.
                //Possibly add a delay here to remove stress on server.
                CommandPaintball paintball = new CommandPaintball();

                int count = PaintballManager.joinedPlayers.Count();
                for (int x = 0; x < count; x++)
                {
                    if(PaintballManager.joinedPlayers[0].getRevived() == false)
                        PaintballManager.joinedPlayers[0].getPlayer().Kick("Paintball was turned off while you were dead. Please rejoin.");
                    else
                        paintball.leave(PaintballManager.joinedPlayers[0].getPlayer());
                }

                PaintballManager.gameRunning = false;
                PaintballManager.paintballOn = false;
            }

            //Set waiting position
            if(command[0].ToLower() == "poswait")
            {
                UnturnedPlayer pCaller = (UnturnedPlayer)caller;

                PaintballManager.waitingPosition = pCaller.Position;
                PaintballManager.waitingRotation = pCaller.Rotation;

                UnturnedChat.Say(caller, "Waiting position set.", Color.green);
                return;
            }

            //Set arena position
            if (command[0].ToLower() == "posarena")
            {
                UnturnedPlayer pCaller = (UnturnedPlayer)caller;

                //If true, do the second position.
                if(arenaPosition1Temp.x != -9999 && arenaPosition1Temp.y != -9999 && arenaPosition1Temp.z != -9999)
                {
                    //Do second position.
                    arenaPosition2Temp = pCaller.Position;

                    //Transfer the positions and reset the temp values.
//                    PaintballManager.arenaPosition1 = new Vector3(arenaPosition1Temp.x, arenaPosition1Temp.y, arenaPosition1Temp.z);
//                    PaintballManager.arenaPosition2 = new Vector3(arenaPosition2Temp.x, arenaPosition2Temp.y, arenaPosition2Temp.z);
                    
                    //Sets min and max for X
                    if(arenaPosition1Temp.x < arenaPosition2Temp.x)
                    {
                        PaintballManager.arenaMinX = (int)arenaPosition1Temp.x;
                        PaintballManager.arenaMaxX = (int)arenaPosition2Temp.x;
                    }
                    else
                    {
                        PaintballManager.arenaMinX = (int)arenaPosition2Temp.x;
                        PaintballManager.arenaMaxX = (int)arenaPosition1Temp.x;
                    }

                    //Sets min and max for y
                    if (arenaPosition1Temp.z < arenaPosition2Temp.z)
                    {
                        PaintballManager.arenaMinZ = (int)arenaPosition1Temp.z;
                        PaintballManager.arenaMaxZ = (int)arenaPosition2Temp.z;
                    }
                    else
                    {
                        PaintballManager.arenaMinZ = (int)arenaPosition2Temp.z;
                        PaintballManager.arenaMaxZ = (int)arenaPosition1Temp.z;
                    }

                    if(arenaPosition1Temp.y < arenaPosition2Temp.y)
                    {
                        PaintballManager.arenaY = (int)arenaPosition1Temp.y;
                    }
                    else
                    {
                        PaintballManager.arenaY = (int)arenaPosition2Temp.y;
                    }

                    arenaPosition1Temp = new Vector3(-9999, -9999, -9999);
                    arenaPosition2Temp = new Vector3(-9999, -9999, -9999);

                    UnturnedChat.Say(caller, "Arena position is now ready.", Color.green);
                }
                else
                {
                    //First position.
                    arenaPosition1Temp = pCaller.Position;
                    UnturnedChat.Say(caller, "Stand in the second position to define the paintball spawn area and call the command again.", Color.green);
                }
                
            }

            //Start the game.
            if(command[0] == "start")
            {
                startPaintball(caller);
            }

            //Stop the game.
            if(command[0] == "stop")
            {
                stopPaintball(caller);
            }

            //Count amount of players in paintball.
            if(command[0] == "list")
            {
                string allJoinedPlayers = "";
                for (int x = 0; x < PaintballManager.joinedPlayers.Count; x++)
                {
                    allJoinedPlayers += PaintballManager.joinedPlayers[x].getName() + " ";
                }
                UnturnedChat.Say(caller, "Players (" + PaintballManager.joinedPlayers.Count() + "): " + allJoinedPlayers, Color.green);
                return;
            }

            //Get wins of a player.
            if(command[0] == "wins")
            {
                if(command[1] == null)
                {
                    UnturnedChat.Say("Syntax error: /paintbalops wins (player)");
                    return;
                }

                UnturnedPlayer currentPlayer = PlayerManager.findPlayer(caller, command[1]);

                int winCount;
                PaintballManager.playerWins.TryGetValue(currentPlayer.CSteamID, out winCount);
                UnturnedChat.Say(caller, currentPlayer.CharacterName + " has won " + winCount + " times.", Color.green);
            }

            //Set a player's wins.
            if(command[0] == "setwins")
            {
                if (command[1] == null)
                {
                    UnturnedChat.Say("Syntax error: /paintbalops setwins (player) (amount)");
                    return;
                }

                UnturnedPlayer currentPlayer = PlayerManager.findPlayer(caller, command[1]);

                PaintballManager.playerWins[currentPlayer.CSteamID] = Convert.ToInt32(command[2]);

                UnturnedChat.Say(caller, currentPlayer.CharacterName + "'s win count has been set to " + command[2] + ".");
            }

            //Check prize list for certain items. /paintballops checkprize (win level)
            if(command[0] == "checkprize")
            {

                string prizeList;

                if (PaintballManager.paintballRewards.TryGetValue(Convert.ToInt32(command[1]), out prizeList))
                    UnturnedChat.Say(caller, "The prize IDs for level " + command[1] + " are: " + prizeList, Color.green);
                else
                    UnturnedChat.Say(caller, "There are no prizes set for level " + command[1] + ".");
            }

            //Set prizes (only set when server not reloaded) /paintballops setprize (level) (list in quotes)
            if(command[0] == "setprize")
            {
                if (command[1] != null && command[2] != null)
                {
                    int level = Convert.ToInt32(command[1]);
                    string currentPrizes;

                    if(PaintballManager.paintballRewards.TryGetValue(level, out currentPrizes))
                    {
                        currentPrizes = command[2].Replace(" ", "");
                        PaintballManager.paintballRewards[level] = currentPrizes;

                        UnturnedChat.Say(caller, "The prize IDs for level " + level + " are: " + currentPrizes, Color.green);
                    }
                    else
                    {
                        currentPrizes = command[2].Replace(" ", "");

                        PaintballManager.paintballRewards.Add(level, currentPrizes);

                        UnturnedChat.Say(caller, "Added a level " + level + " with prizes: " + currentPrizes, Color.green);
                    }
                }
                else
                    UnturnedChat.Say(caller, "Error, format is /paintballops setprize (win count) \"(item IDs separated by commas\"", Color.green);
            }

            if(command[0] == "alive")
            {
                UnturnedChat.Say(caller, "There are " + PaintballManager.alive + " players still alive.");
                return;
            }
        }

        //Start paintball method
        public void startPaintball(IRocketPlayer caller)
        {
            if(PaintballManager.gameRunning)
            {
                UnturnedChat.Say(caller, "Paintball already has an active game running.", Color.red);
                return;
            }

            //Check if anyone hasn't respawned
            for (int x = 0; x < PaintballManager.joinedPlayers.Count(); x++)
            {
                if (PaintballManager.joinedPlayers[x].getRevived() == false)
                {
                    PaintballManager.joinedPlayers[x].getPlayer().Kick("AFK during paintball. Please rejoin.");
                }
            }

            if (PaintballManager.joinedPlayers.Count() == 1)
            {
                UnturnedChat.Say(caller, "Cannot start game, only one player is joined.", Color.red);
                PaintballManager.gameRunning = false;
                return;
            }

            for (int x = 0; x < PaintballManager.joinedPlayers.Count(); x++)
            {
                //Create the player
                UnturnedPlayer currentPlayer = PaintballManager.joinedPlayers[x].getPlayer();

                //Set dead to false
                PaintballManager.joinedPlayers[x].setDead(false);

                //Turn off god, clear, heal, and give max skills.
                currentPlayer.GodMode = false;
                PlayerManager.clearInventory(currentPlayer); ;
                currentPlayer.Heal(100);
                currentPlayer.Hunger = 0;
                currentPlayer.Thirst = 0;
                currentPlayer.Infection = 0;
                PlayerManager.maxSkills(currentPlayer);

                //Spawn the paintball items
                currentPlayer.Inventory.tryAddItem(UnturnedItems.AssembleItem(1337, 250, new Attachment(1004, 100), new Attachment(151, 100), new Attachment(8, 100), new Attachment(1338, 1), new Attachment(1340, 100), EFiremode.SEMI), true);
                currentPlayer.GiveItem(1048, 1);
                currentPlayer.GiveItem(394, 4);
                currentPlayer.GiveItem(1133, 1);
                currentPlayer.GiveItem(431, 1);
                currentPlayer.GiveItem(177, 1);
                currentPlayer.GiveItem(548, 1);
                currentPlayer.GiveItem(1340, 5);

                //Locate their spawn point.
                System.Random spawnGen = new System.Random();
                Vector3 playerSpawn = new Vector3(spawnGen.Next(PaintballManager.arenaMinX, PaintballManager.arenaMaxX), PaintballManager.arenaY, spawnGen.Next(PaintballManager.arenaMinZ, PaintballManager.arenaMaxZ));
                //Transport the player
                currentPlayer.Teleport(playerSpawn, spawnGen.Next(0, 360));
            }
            PaintballManager.gameRunning = true;
        }

        public static void stopPaintball(IRocketPlayer caller)
        {
            if(PaintballManager.gameRunning)
            {
                for(int x = 0; x < PaintballManager.joinedPlayers.Count(); x++)
                {
                    UnturnedPlayer currentPlayer = PaintballManager.joinedPlayers[x].getPlayer();

                    //Turn on god
                    currentPlayer.GodMode = true;

                    //Clear them
                    PlayerManager.clearInventory(currentPlayer);

                    //Send them to waiting room.
                    currentPlayer.Teleport(PaintballManager.waitingPosition, PaintballManager.waitingRotation);

                    //Let the user know, it was manually stopped..
                    UnturnedChat.Say(currentPlayer, "The paintball game was stopped.", Color.green);
                }
                PaintballManager.gameRunning = false;
                UnturnedChat.Say(caller, "The game has stopped.", Color.green);
            }
            else
            {
                //Called when the current game naturally ends.
                PaintballManager.gameRunning = false;
            }
        }
    }
}
