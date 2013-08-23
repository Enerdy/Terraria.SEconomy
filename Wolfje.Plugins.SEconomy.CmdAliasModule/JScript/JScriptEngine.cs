using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wolfje.Plugins.SEconomy.CmdAliasModule.JScript {
    internal class JScriptEngine {

        internal static readonly List<JScriptAliasCommand> jsAliases = new List<JScriptAliasCommand>();
        internal static readonly Random randomGenerator = new Random();
        internal static readonly object __rndLock = new object();

        #region "delegates"

        internal delegate void RegisterCommandDelegate(string AliasName, string Cost, double CooldownSeconds, string Permissions, Jint.Native.JsFunction func);

        internal delegate void TransferAsyncDelegate(Journal.XBankAccount FromAccount, Journal.XBankAccount ToAccount, Money Amount, string journalMessage, Jint.Native.JsFunction completedFunc);

        #endregion

        internal static void Initialize() {
            CmdAliasPlugin.scriptEngine = new Jint.JintEngine();
            CmdAliasPlugin.scriptEngine.DisableSecurity();
            CmdAliasPlugin.scriptEngine.SetDebugMode(true);

            ///function seconomy_transfer_async(toAccount : Journal.XBankAccount, player : TShockAPI.TSPlayer, amount : string, completedCallback : function)
            ///
            ///Asynchronously transfers money using SEconomy, then calls back to the JS function specified by completedCallback
            CmdAliasPlugin.scriptEngine.SetFunction("seconomy_transfer_async", new TransferAsyncDelegate((from, to, amount, msg, func) => {
                from.TransferToAsync(to, amount, Journal.BankAccountTransferOptions.AnnounceToSender, Message: msg).ContinueWith((task) => {
                    //callback to the JS function with the result of the transfer
                    CmdAliasPlugin.scriptEngine.CallFunction(func, task.Result);
                });
            }));

            CmdAliasPlugin.scriptEngine.SetFunction("seconomy_pay_async", new TransferAsyncDelegate((from, to, amount, msg, func) => {
                from.TransferToAsync(to, amount, Journal.BankAccountTransferOptions.AnnounceToReceiver | Journal.BankAccountTransferOptions.AnnounceToSender | Journal.BankAccountTransferOptions.IsPayment, Message: msg).ContinueWith((task) => {
                    //callback to the JS function with the result of the transfer
                    CmdAliasPlugin.scriptEngine.CallFunction(func, task.Result);
                });
            }));

            CmdAliasPlugin.scriptEngine.SetFunction("seconomy_parse_money", new Func<object, Money>((moneyString) => {
                return Money.Parse(moneyString.ToString());
            }));

            ///function seconomy_valid_money(moneyString : string) : boolean
            ///
            ///Returns true if a supplied money value is valid and is parsable or not.
            CmdAliasPlugin.scriptEngine.SetFunction("seconomy_valid_money", new Func<object, bool>((moneyString) => {
                Money _money;
                return Money.TryParse(moneyString.ToString(), out _money);
            }));



            CmdAliasPlugin.scriptEngine.SetFunction("seconomy_get_account", new Func<object, Journal.XBankAccount>((accountName) => {

                if (accountName is TShockAPI.TSPlayer) {
                    return SEconomyPlugin.GetEconomyPlayerSafe((accountName as TShockAPI.TSPlayer).Name).BankAccount;
                } else {
                    return SEconomyPlugin.GetEconomyPlayerSafe(accountName.ToString()).BankAccount;
                }

            }));


            ///global variable: __world_account
            ///
            ///Returns a reference to the SEconomy world account
            CmdAliasPlugin.scriptEngine.SetFunction("seconomy_world_account", new Func<Journal.XBankAccount>(() => {
                return SEconomyPlugin.WorldAccount;
            }));

            ///function create_alias(aliasName : string, commandCost : string, cooldownSeconds : integer, permissionsNeeded : string, functionToExecute : function)
            ///
            ///Creates an alias that executes the specified functionToExecute in js when it is called by a player/server.
            CmdAliasPlugin.scriptEngine.SetFunction("create_alias", new RegisterCommandDelegate((aliasname, cost, cooldown, perms, func) => {
                JScriptAliasCommand jAlias = new JScriptAliasCommand() { CommandAlias = aliasname, CooldownSeconds = Convert.ToInt32(cooldown), Cost = cost, Permissions = perms, func = func };

                jsAliases.RemoveAll(i => i.CommandAlias == aliasname);
                jsAliases.Add(jAlias);
            }));

            /// function log(logEntry : string)
            /// 
            ///Writes logEntry to the TShock log
            CmdAliasPlugin.scriptEngine.SetFunction<string>("log", (logText) => {
                TShockAPI.Log.ConsoleInfo(logText);
            });

            ///function get_player(playerName : string) : TShockAPI.TSPlayer
            ///
            ///Returns a TSPlayer by their characterName.  Returns undefined if the player isn't found or is offline.
            CmdAliasPlugin.scriptEngine.SetFunction("get_player", new Func<string, TShockAPI.TSPlayer>((name) => {
                return TShockAPI.TShock.Players.FirstOrDefault(i => i.Name == name);
            }));

            ///function random(from : integer, to : integer) : integer
            ///
            ///Returns a random number between from and to.
            CmdAliasPlugin.scriptEngine.SetFunction("random", new Func<double, double, double>((rndFrom, rndTo) => {
                lock (__rndLock) {
                    int from = Convert.ToInt32(rndFrom);
                    int to = Convert.ToInt32(rndTo);
                    return randomGenerator.Next(from, to);
                }
            }));

            //function group_exists(groupName : string) : boolean
            //
            //Returns whether the TShock group specified by groupName exists
            CmdAliasPlugin.scriptEngine.SetFunction("group_exists", new Func<object, bool>((groupName) => {
                return TShockAPI.TShock.Groups.Count(i => i.Name.Equals(groupName.ToString(), StringComparison.CurrentCultureIgnoreCase)) > 0;
            }));

            //tshock_group(groupName : string) : TShockAPI.Group
            //
            //Returns a TShock Group object by its name
            CmdAliasPlugin.scriptEngine.SetFunction("tshock_group", new Func<object, TShockAPI.Group>((groupName) => {
                if (groupName == null) {
                    return null;
                }

                TShockAPI.Group g = TShockAPI.TShock.Groups.FirstOrDefault(i => i.Name.Equals(groupName.ToString(), StringComparison.CurrentCultureIgnoreCase));
                return g;
            }));

            //function execute_command(player : TShockAPI.TSPlayer, command : string)
            //
            //causes player to execute provided command in the TShock execution handler.
            //this function ignores permissions, and will always execute as though the user has permissions to do it.
            CmdAliasPlugin.scriptEngine.SetFunction("execute_command", new Action<TShockAPI.TSPlayer, object>((player, cmd) => {
                string commandToExecute = "";
                if (cmd is List<string>) {
                    List<string> cmdList = cmd as List<string>;

                    foreach (var param in cmdList.Skip(1)) {
                        commandToExecute += " " + param;
                    }
                } else if (cmd is string) {
                    commandToExecute = cmd.ToString();
                }
                
                CmdAliasPlugin.HandleCommandWithoutPermissions(player, string.Format("{0}", commandToExecute.Trim()));
            }));

            //function change_group(player : TShockAPI.TSPlayer, groupName : string)
            //
            //Changes the player's TShock group to the group provided by groupName
            CmdAliasPlugin.scriptEngine.SetFunction("change_group", new Action<TShockAPI.TSPlayer, object>((player, group) => {
                TShockAPI.DB.User u = new TShockAPI.DB.User();
                string g = "";

                if (group is string) {
                    g = group as string;
                } else if (group is TShockAPI.Group) {
                    g = (group as TShockAPI.Group).Name;
                }

                if (player != null && !string.IsNullOrEmpty(g)) {
                    u.Name = player.UserAccountName;
                    TShockAPI.TShock.Users.SetUserGroup(u, g);
                }

            }));

            ///function msg(player : TShockAPI.TSPlayer, message : string)
            ///
            ///Sends an informational message to a player.
            CmdAliasPlugin.scriptEngine.SetFunction("msg", new Action<TShockAPI.TSPlayer, object>((player, msg) => {
                if (player != null && msg != null) {
                    player.SendInfoMessageFormat("{0}", msg);
                }
            }));

            ///function broadcast(msg : string)
            ///
            ///Sends a server broadcast.
            CmdAliasPlugin.scriptEngine.SetFunction("broadcast", new Action<object>((msg) => {
                if (msg != null) {
                    TShockAPI.TShock.Utils.Broadcast("(Server Broadcast) " + msg.ToString(), Color.Red);
                }
            }));
        }

    }
}
