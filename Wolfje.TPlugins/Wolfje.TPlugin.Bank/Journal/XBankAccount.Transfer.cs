using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wolfje.Plugins.SEconomy.Journal {
    public partial class XBankAccount {

        /// <summary>
        /// Transaction synch object, only one transaction may write to the journal at a time.
        /// </summary>
        public static readonly object __tranlock = new object();

        /// <summary>
        /// Returns whether a transfer is allowed to succeed or not.
        /// </summary>
        public static bool TransferMaySucceed(XBankAccount FromAccount, XBankAccount ToAccount, Money MoneyNeeded, Journal.BankAccountTransferOptions Options) {
            if (FromAccount == null || ToAccount == null) {
                return false;
            }

            return ((FromAccount.IsSystemAccount || FromAccount.IsPluginAccount || ((Options & Journal.BankAccountTransferOptions.AllowDeficitOnNormalAccount) == Journal.BankAccountTransferOptions.AllowDeficitOnNormalAccount)) || (FromAccount.Balance >= MoneyNeeded && MoneyNeeded > 0));
        }

        XTransaction BeginSourceTransaction(Money Amount, string Message) {
            XTransaction sourceTran = new XTransaction(Amount);

            sourceTran.BankAccountFK = this.BankAccountK;
            sourceTran.Flags = Journal.BankAccountTransactionFlags.FundsAvailable;
            sourceTran.TransactionDateUtc = DateTime.UtcNow;
            sourceTran.Amount = (Amount * (-1));

            if (!string.IsNullOrEmpty(Message)) {
                sourceTran.Message = Message;
            }

            lock (TransactionJournal.XmlJournal) {
                return Journal.TransactionJournal.AddTransaction(sourceTran);
            }
        }

        XTransaction FinishEndTransaction(string SourceBankTransactionKey, XBankAccount ToAccount, Money Amount, string Message) {
            XTransaction destTran = new XTransaction(Amount);

            destTran.BankAccountFK = ToAccount.BankAccountK;
            destTran.Flags = Journal.BankAccountTransactionFlags.FundsAvailable;
            destTran.TransactionDateUtc = DateTime.UtcNow;
            destTran.Amount = Amount;
            destTran.BankAccountTransactionFK = SourceBankTransactionKey;

            if (!string.IsNullOrEmpty(Message)) {
                destTran.Message = Message;
            }

            lock (TransactionJournal.XmlJournal) {
                return Journal.TransactionJournal.AddTransaction(destTran);
            }
        }

        void BindTransactions(ref XTransaction SourceTransaction, ref XTransaction DestTransaction) {

            lock (TransactionJournal.XmlJournal) {
                SourceTransaction.BankAccountTransactionFK = DestTransaction.BankAccountTransactionK;
                DestTransaction.BankAccountTransactionFK = SourceTransaction.BankAccountTransactionK;
            }

        }

        /// <summary>
        /// Asynchronously transfers to another account.
        /// </summary>
        public Task<BankTransferEventArgs> TransferToAsync(XBankAccount ToAccount, Money Amount, BankAccountTransferOptions Options, string TransactionMessage, string JournalMessage) {
            Guid profile = SEconomyPlugin.Profiler.Enter(string.Format("transferAsync: {0} to {1}", this.UserAccountName, ToAccount.UserAccountName));
            return Task.Factory.StartNew<BankTransferEventArgs>(() => {
                BankTransferEventArgs args = TransferTo(ToAccount, Amount, Options, TransactionMessage, JournalMessage);
                return args;
            }).ContinueWith((task) => {
                SEconomyPlugin.Profiler.ExitLog(profile);
                return task.Result;
            });
        }


        /// <summary>
        /// Asynchronously transfers to another account.
        /// </summary>
        public Task<BankTransferEventArgs> TransferToAsync(int Index, Money Amount, BankAccountTransferOptions Options, string TransactionMessage, string JournalMessage) {
            Economy.EconomyPlayer ePlayer = SEconomyPlugin.GetEconomyPlayerSafe(Index);

            Guid profile = SEconomyPlugin.Profiler.Enter(string.Format("transferAsync: {0} to {1}", this.UserAccountName, ePlayer.BankAccount != null ? ePlayer.BankAccount.UserAccountName : "Unknown"));
            return Task.Factory.StartNew<BankTransferEventArgs>(() => {
                BankTransferEventArgs args = TransferTo(ePlayer.BankAccount, Amount, Options, TransactionMessage, JournalMessage);
                return args;
            }).ContinueWith((task) => {
                SEconomyPlugin.Profiler.ExitLog(profile);
                return task.Result;
            });
        }

        /// <summary>
        /// Transfers money from this account to the destination account, if negative, takes money from the destination account into this account.
        /// </summary>
        public BankTransferEventArgs TransferTo(XBankAccount ToAccount, Money Amount, BankAccountTransferOptions Options, string TransactionMessage, string JournalMessage) {
            BankTransferEventArgs args = new BankTransferEventArgs();
            Guid profile = Guid.Empty;

            try {
                lock (__tranlock) {
                  
                    if (ToAccount != null) {
                        if (TransferMaySucceed(this, ToAccount, Amount, Options)) {
                            if (SEconomyPlugin.Configuration.EnableProfiler) {
                                profile = SEconomyPlugin.Profiler.Enter(string.Format("transfer: {0} to {1}", !string.IsNullOrEmpty(this.UserAccountName) ? this.UserAccountName : "Unknown", ToAccount != null && !string.IsNullOrEmpty(ToAccount.UserAccountName) ? ToAccount.UserAccountName : "Unknown"));
                            }
                            args.Amount = Amount;
                            args.SenderAccount = this;
                            args.ReceiverAccount = ToAccount;
                            args.TransferOptions = Options;
                            args.TransferSucceeded = false;
                            args.TransactionMessage = TransactionMessage;

                            //insert the source negative transaction
                            XTransaction sourceTran = BeginSourceTransaction(Amount, JournalMessage);
                            if (sourceTran != null && !string.IsNullOrEmpty(sourceTran.BankAccountTransactionK)) {
                                //insert the destination inverse transaction
                                XTransaction destTran = FinishEndTransaction(sourceTran.BankAccountTransactionK, ToAccount, Amount, JournalMessage);

                                if (destTran != null && !string.IsNullOrEmpty(destTran.BankAccountTransactionK)) {
                                    //perform the double-entry binding
                                    BindTransactions(ref sourceTran, ref destTran);

                                    args.TransactionID = sourceTran.BankAccountTransactionK;

                                    //update balances
                                    this.Balance += (Amount * (-1));
                                    ToAccount.Balance += Amount;

                                    //transaction complete
                                    args.TransferSucceeded = true;
                                }
                            }
                        } else {
                            args.TransferSucceeded = false;

                            //concept: ??????
                            //if the amount coming from "this" account is a negative then the "sender account" needs to know the transfer failed.
                            //if the amount coming from "this" acount is a positive then the "reciever account" needs to know the transfer failed.

                            if (!ToAccount.IsSystemAccount && !ToAccount.IsPluginAccount) {
                                if (this.Owner != null) {
                                    if (Amount < 0) {
                                        this.Owner.TSPlayer.SendErrorMessageFormat("Invalid amount.");
                                    } else {
                                        this.Owner.TSPlayer.SendErrorMessageFormat("You need {0} more money to make this payment.", ((Money)(this.Balance - Amount)).ToLongString());
                                    }
                                }
                            }
                        }
                    }

                    //raise the transfer event
                    OnBankTransferComplete(args);

                    if (SEconomyPlugin.Configuration.EnableProfiler) {
                        SEconomyPlugin.Profiler.ExitLog(profile);
                    }
                }

            } catch (Exception ex) {
                args.Exception = ex;
                args.TransferSucceeded = false;
            }

            return args;
        }
    }
}
