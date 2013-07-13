﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Reflection;
using Terraria;

using Wolfje.Plugins.SEconomy.ModuleFramework;
using System.Threading.Tasks;

namespace Wolfje.Plugins.SEconomy.Modules.VaultEx {

    [SEconomyModule]
    public class VaultEx : ModuleBase {

        public static List<VaultPlayer> PlayerList { get; set; }
        internal static List<BossNPC> BossList { get; set; }

        internal static VaultConfig Config { get; set; }

        internal static readonly Random _r = new Random();

        public VaultEx() { }

        //public VaultEx(SEconomyPlugin plugin)
        //    : base(plugin) {

        //}

        #region "API Plugin Stub"
        public override string Author {
            get {
                return "InanZen (adapted by Wolfje)";
            }
        }

        public override string Description {
            get {
                return "Vault drop-in replacement for server-side economy.";
            }
        }

        public override string Name {
            get {
                return "VaultEx for SEconomy";
            }
        }

        public override Version Version {
            get {
                return Assembly.GetExecutingAssembly().GetName().Version;
            }
        }

        #endregion



        public override void Initialize() {

            PlayerList = new List<VaultPlayer>();
            BossList = new List<BossNPC>();

            Config = VaultConfig.ReadConfig(this.ConfigFilePath);

            Hooks.NetHooks.GetData += NetHooks_GetData;
            Hooks.NetHooks.SendData += NetHooks_SendData;
            Hooks.ServerHooks.Join += ServerHooks_Join;

        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                Hooks.NetHooks.GetData -= NetHooks_GetData;
                Hooks.NetHooks.SendData -= NetHooks_SendData;
                Hooks.ServerHooks.Join -= ServerHooks_Join;
            }
            
            base.Dispose(disposing);
        }

        void EconomyPlayer_PlayerBankAccountLoaded(object sender, EventArgs e) {
            SEconomy.Economy.BankAccount ba = sender as SEconomy.Economy.BankAccount;
            Console.WriteLine(ba.Owner.TSPlayer.Name);
        }

        void ServerHooks_Join(int who, System.ComponentModel.HandledEventArgs arg2) {
            PlayerList.Add(new VaultPlayer(who));
        }

        /// <summary>
        /// Occurs when the server sends data.
        /// </summary>
        void NetHooks_SendData(Hooks.SendDataEventArgs e) {
            if (e.MsgID == PacketTypes.NpcStrike) {
                NPC npc = Main.npc[e.number];

                if (npc != null) {

                    if (npc.boss) {

                        if (npc.life <= 0) {

                            for (int i = BossList.Count - 1; i >= 0; i--) {
                                if (BossList[i].Npc == null)
                                    BossList.RemoveAt(i);
                                else if (BossList[i].Npc == npc) {
                                    var rewardDict = BossList[i].GetRecalculatedReward();

                                    foreach (KeyValuePair<int, long> reward in rewardDict) {
                                        if (PlayerList[reward.Key] != null) {

                                            SEconomy.Economy.EconomyPlayer ePlayer = SEconomyPlugin.GetEconomyPlayerSafe(reward.Key);
                                            if (ePlayer != null) {
                                                //Pay from the world account to the reward recipient.
                                                Economy.BankAccountTransferOptions options = Economy.BankAccountTransferOptions.None;

                                                if (Config.AnnounceBossGain) {
                                                    options |= Economy.BankAccountTransferOptions.AnnounceToReceiver;
                                                }

                                                SEconomyPlugin.WorldAccount.TransferAndReturn(ePlayer.BankAccount, reward.Value, options);
                                            }
                                        }

                                    }
                                    BossList.RemoveAt(i);
                                } else if (!BossList[i].Npc.active)
                                    BossList.RemoveAt(i);
                            }

                            if (e.ignoreClient >= 0) {
                                var player = PlayerList[e.ignoreClient];
                                if (player != null)
                                    player.AddKill(npc.netID);
                            }
                        } else if (e.ignoreClient >= 0) {
                            var bossnpc = BossList.Find(n => n.Npc == npc);
                            if (bossnpc != null)
                                bossnpc.AddDamage(e.ignoreClient, (int)e.number2);
                            else {
                                BossNPC newBoss = new BossNPC(npc);
                                newBoss.AddDamage(e.ignoreClient, (int)e.number2);
                                BossList.Add(newBoss);
                            }
                        }
                    } else if (npc.life <= 0 && e.ignoreClient >= 0) {
                        var player = PlayerList[e.ignoreClient];
                        if (player != null) {
                            if (npc.value > 0) {
                                float Mod = 1;
                                if (player.TSPlayer.TPlayer.buffType.Contains(13)) { // battle potion
                                    Mod *= Config.BattlePotionModifier;
                                }
                                if (Config.OptionalMobModifier.ContainsKey(npc.netID)) {
                                    Mod *= Config.OptionalMobModifier[npc.netID]; // apply custom modifiers                                        
                                }


                                int minVal = (int)((npc.value - (npc.value * 0.1)) * Mod);
                                int maxVal = (int)((npc.value + (npc.value * 0.1)) * Mod);
                                int rewardAmt = _r.Next(minVal, maxVal);

                                int i = player.TSPlayer.Index;

                                SEconomy.Economy.EconomyPlayer epl = SEconomyPlugin.GetEconomyPlayerSafe(i);
                                Economy.BankAccountTransferOptions options = Economy.BankAccountTransferOptions.None;

                                if (Config.AnnounceKillGain) {
                                    options |= Economy.BankAccountTransferOptions.AnnounceToReceiver;
                                }

                                Task shuttingTheCompilerWarningUp = SEconomyPlugin.WorldAccount.TransferToPlayerAsync(i, rewardAmt, options);

                                // player.ChangeMoney(rewardAmt, MoneyEventFlags.Kill, config.AnnounceKillGain);
                            }
                            player.AddKill(npc.netID);
                        }
                    }
                }
            } else if (e.MsgID == PacketTypes.PlayerKillMe) {
                //Console.WriteLine("(SendData) PlayerKillMe -> 1:{0} 2:{4} 3:{5} 4:{6} 5:{1} remote:{2} ignore:{3}", e.number, e.number5, e.remoteClient, e.ignoreClient, e.number2, e.number3, e.number4);
                // 1-playerID, 2-direction, 3-dmg, 4-PVP
                var deadPlayer = PlayerList[e.number];
                Economy.EconomyPlayer eDeadPlayer = SEconomyPlugin.GetEconomyPlayerSafe(e.number);

                if (deadPlayer != null) {
                    long penaltyAmmount = 0;

                    if (Config.StaticDeathPenalty) {
                        penaltyAmmount = _r.Next(Config.DeathPenaltyMin, Config.DeathPenaltyMax);
                    } else if ( eDeadPlayer.BankAccount != null )  {
                        penaltyAmmount = (long)(eDeadPlayer.BankAccount.Money * (Config.DeathPenaltyPercent / 100f));
                    }

                    //   Console.WriteLine("penalty ammount: {0}", penaltyAmmount);
                    if (e.number4 == 1) {
                        if (!deadPlayer.TSPlayer.Group.HasPermission("vault.bypass.death") /* && deadPlayer.ChangeMoney(-penaltyAmmount, MoneyEventFlags.PvP, true) */ && Config.PvPWinnerTakesLoosersPenalty && deadPlayer.LastPVPID != -1) {
                            var killer = PlayerList[deadPlayer.LastPVPID];
                            Economy.EconomyPlayer eKiller = SEconomyPlugin.GetEconomyPlayerSafe(deadPlayer.LastPVPID);
                            
                            if (eKiller != null && eKiller.BankAccount != null) {
                                Economy.BankAccountTransferOptions options = Economy.BankAccountTransferOptions.MoneyFromPvP | Economy.BankAccountTransferOptions.AnnounceToReceiver | Economy.BankAccountTransferOptions.AnnounceToSender;

                              //  killer.ChangeMoney(penaltyAmmount, MoneyEventFlags.PvP, true);

                                //Here in PVP the loser pays the winner money out of their account.
                                Task shuttingTheCompilerWarningUp = eDeadPlayer.BankAccount.TransferToPlayerAsync(deadPlayer.LastPVPID, penaltyAmmount, options);
                            }
                        }
                    } else if (!deadPlayer.TSPlayer.Group.HasPermission("vault.bypass.death")) {
                        Economy.BankAccountTransferOptions options = Economy.BankAccountTransferOptions.MoneyFromPvP | Economy.BankAccountTransferOptions.AnnounceToReceiver | Economy.BankAccountTransferOptions.AnnounceToSender;

                       // deadPlayer.ChangeMoney(-penaltyAmmount, MoneyEventFlags.Death, true);

                        Task shuttingTheCompilerWarningUp = SEconomyPlugin.WorldAccount.TransferToPlayerAsync(deadPlayer.Index, -penaltyAmmount, options);
                    }
                }
            } else if (e.MsgID == PacketTypes.PlayerDamage) {
                // Console.WriteLine("(SendData) PlayerDamage -> 1:{0} 2:{4} 3:{5} 4:{6} 5:{1} remote:{2} ignore:{3}", e.number, e.number5, e.remoteClient, e.ignoreClient, e.number2, e.number3, e.number4);
                // 1: pID, ignore: Who, 2: dir, 3:dmg, 4:pvp;
                if (e.number4 == 1) { // if PvP {
                    var player = PlayerList[e.number];

                    if (player != null) {
                        player.LastPVPID = e.ignoreClient;
                    }
                }
            }
        }



        /// <summary>
        /// Occurs when the server receives data.
        /// </summary>
        void NetHooks_GetData(Hooks.GetDataEventArgs e) {

            if (e.MsgID == PacketTypes.PlayerUpdate) {
                byte plyID = e.Msg.readBuffer[e.Index];
                byte flags = e.Msg.readBuffer[e.Index + 1];

                var player = PlayerList[plyID];
                if (player != null && player.LastState != flags) {
                    player.LastState = flags;
                    player.IdleCount = 0;
                }
            }
        }


    }
}
