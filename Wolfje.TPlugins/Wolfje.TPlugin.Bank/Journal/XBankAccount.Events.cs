using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wolfje.Plugins.SEconomy.Journal
{
    public partial class XBankAccount
    {
	/// <summary>
        /// Raises when a bank accounts flags change
        /// </summary>
        public static event EventHandler<BankAccountChangedEventArgs> BankAccountFlagsChanged;

        /// <summary>
        /// Raises when a bank account balance changes.
        /// </summary>
        public static event EventHandler<MoneyChangedEventArgs> MoneyChanged;

        /// <summary>
        /// Raises when a bank transfer has been completed.
        /// </summary>
        public static event EventHandler<BankTransferEventArgs> BankTransferCompleted;

        /// <summary>
        /// Raises the BankAccountFlagsChanged event
        /// </summary>
        void OnBankAccountChanged(BankAccountChangedEventArgs Args) {
            if (BankAccountFlagsChanged != null) {
                BankAccountFlagsChanged(this, Args);
            }
        }

        /// <summary>
        /// Raises the BankTransferComplete event
        /// </summary>
        /// <param name="Args"></param>
        void OnBankTransferComplete(BankTransferEventArgs Args) {
            if (BankTransferCompleted != null) {
                BankTransferCompleted(this, Args);
            }
        }

        /// <summary>
        /// Raises the MoneyChanged event
        /// </summary>
        void OnMoneyChanged(MoneyChangedEventArgs Args) {
            if (MoneyChanged != null) {
                MoneyChanged(this, Args);
            }
        }
    }

    /// <summary>
    /// Holds information about what the bank account changes were
    /// </summary>
    public class BankAccountChangedEventArgs : EventArgs {
        /// <summary>
        /// Index of the TSPlayer that initiated the change of the account
        /// </summary>
        public int CallerID { get; set; }
        /// <summary>
        /// What the flags changed to.
        /// </summary>
        public Journal.BankAccountFlags NewFlags { get; set; }
    }

    /// <summary>
    /// Describes the money that has changed
    /// </summary>
    public class MoneyChangedEventArgs : EventArgs {
        public Money OldMoney { get; set; }
        public Economy.EconomyPlayer Initiator { get; set; }
    }

    /// <summary>
    /// Describes a bank account transaction
    /// </summary>
    public class BankTransferEventArgs : EventArgs {
        public bool TransferSucceeded { get; set; }
        public XBankAccount ReceiverAccount { get; set; }
        public XBankAccount SenderAccount { get; set; }
        public string TransactionID { get; set; }
        public Money Amount { get; set; }
        public Exception Exception { get; set; }
        public Journal.BankAccountTransferOptions TransferOptions { get; set; }
    }
}
