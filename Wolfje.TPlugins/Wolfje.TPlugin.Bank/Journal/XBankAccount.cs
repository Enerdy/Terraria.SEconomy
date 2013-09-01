using System;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Linq;
using System.Collections;
using System.Threading.Tasks;

namespace Wolfje.Plugins.SEconomy.Journal {

    /// <summary>
    /// Bank Account flags, sets things like enabled, system bank account, etc
    /// </summary>
    [Flags]
    public enum BankAccountFlags {
        Enabled = 1,
        SystemAccount = 1 << 1,
        LockedToWorld = 1 << 2,
        PluginAccount = 1 << 3,
    }

    /// <summary>
    /// A list of options to consider when making a bank transfer.
    /// </summary>
    [Flags]
    public enum BankAccountTransferOptions {
        /// <summary>
        /// None, indicates a silent, normal payment.
        /// </summary>
        None = 0,
        /// <summary>
        /// Announces the payment to the reciever that they recieved, or gained money
        /// </summary>
        AnnounceToReceiver = 1,
        /// <summary>
        /// Announces the payment to the sender that they sent, or paid money
        /// </summary>
        AnnounceToSender = 1 << 1,
        /// <summary>
        /// Overrides the normal deficit logic, and will allow a normal player account to go into 
        /// </summary>
        AllowDeficitOnNormalAccount = 1 << 2,
        /// <summary>
        /// Indicates that the transfer happened because of PvP.
        /// </summary>
        MoneyFromPvP = 1 << 3,

        /// <summary>
        /// Indicates that the money was taken from the player because they died.
        /// </summary>
        MoneyTakenOnDeath = 1 << 4,

        /// <summary>
        /// Indicates that this transfer is a player-to-player transfer.
        /// 
        /// Note that PVP penalties ARE a player to player transfer but are forcefully taken; this is NOT set for these type of transfers, set MoneyFromPvP instead.
        /// </summary>
        IsPlayerToPlayerTransfer = 1 << 5,

        /// <summary>
        /// Indicates that this transaction was a payment for something tangible.
        /// </summary>
        IsPayment = 1 << 6,

        /// <summary>
        /// Suppresses the default announce messages.  Used for modules that have their own announcements for their own transfers.
        /// 
        /// Handle BankAccount.BankTransferSucceeded to hook your own.
        /// </summary>
        SuppressDefaultAnnounceMessages = 1 << 7
    }

    public partial class XBankAccount {
        private XElement _element;
        private long _balance;

        #region "Bank Account Properties"

        /// <summary>
        /// Returns the unique bank account key.
        /// </summary>
        public string BankAccountK {
            get {
                return _element.Attribute("BankAccountK").Value;
            }
            set {
                _element.SetAttributeValue("BankAccountK", value);
            }
        }

        /// <summary>
        /// Returns the bank account username for the player associated with this account.
        /// </summary>
        public string UserAccountName {
            get {
                return _element.Attribute("UserAccountName").Value;
            }
            set {
                _element.SetAttributeValue("UserAccountName", value);
            }
        }

        /// <summary>
        /// The WorldID this account belongs to.
        /// </summary>
        public long WorldID {
            get {
                return long.Parse(_element.Attribute("WorldID").Value);
            }
            set {
                _element.SetAttributeValue("WorldID", value.ToString());
            }
        }

        /// <summary>
        /// The BankAccoubnt flags of this account
        /// </summary>
        public BankAccountFlags Flags {
            get {
                BankAccountFlags flags;
                Enum.TryParse<BankAccountFlags>(_element.Attribute("Flags").Value, out flags);
                return flags;
            }
            set {
                _element.SetAttributeValue("Flags", value.ToString());
            }
        }

        /// <summary>
        /// The description of this bank account
        /// </summary>
        public string Description {
            get {
                return _element.Attribute("Description").Value;
            }
            set {
                _element.SetAttributeValue("Description", value);
            }
        }

        #endregion

        #region "Account Flags"

        /// <summary>
        /// This is shit as fuck and is likely to change.  Payer is a reftype and really needs to be passed into here
        /// </summary>
        public Economy.EconomyPlayer Owner {
            get {
                return SEconomyPlugin.GetEconomyPlayerByBankAccountNameSafe(this.UserAccountName);
            }
        }

        /// <summary>
        /// Returns if this account is enabled
        /// </summary>
        public bool IsAccountEnabled {
            get {
                return (this.Flags & Journal.BankAccountFlags.Enabled) == Journal.BankAccountFlags.Enabled;
            }
        }

        /// <summary>
        /// Returns if this account is a system (world) account
        /// </summary>
        public bool IsSystemAccount {
            get {
                return (this.Flags & Journal.BankAccountFlags.SystemAccount) == Journal.BankAccountFlags.SystemAccount;
            }
        }

        /// <summary>
        /// Returns if this account is locked to the world it was created in
        /// </summary>
        public bool IsLockedToWorld {
            get {
                return (this.Flags & Journal.BankAccountFlags.LockedToWorld) == Journal.BankAccountFlags.LockedToWorld;
            }
        }

        /// <summary>
        /// Returns if this account is a plugin account
        /// </summary>
        public bool IsPluginAccount {
            get {
                return (this.Flags & Journal.BankAccountFlags.PluginAccount) == Journal.BankAccountFlags.PluginAccount;
            }
        }

        /// <summary>
        /// Enables or disables the account.
        /// </summary>
        public void SetAccountEnabled(int CallerID, bool Enabled) {
            Journal.BankAccountFlags _newFlags = this.Flags;

            if (Enabled == false) {
                _newFlags &= (~Journal.BankAccountFlags.Enabled);
            } else {
                _newFlags |= Journal.BankAccountFlags.Enabled;
            }

        }

        #endregion

        #region "Constructors and toll-free bridging"

        /// <summary>
        /// Creates a new XBankAccount element with the specified bank account parameters.
        /// </summary>
        public XBankAccount(string UserAccountName, long WorldID, BankAccountFlags Flags, string Description) {
            this._element = new XElement("BankAccount");

            this.UserAccountName = UserAccountName;
            this.WorldID = WorldID;
            this.Flags = Flags;
            this.Description = Description;
        }

        /// <summary>
        /// Private constructor: creates an XBankAccount from an implicit conversion from Xelement
        /// </summary>
        XBankAccount(XElement element) {
            this._element = element;
        }

        /// <summary>
        /// Returns the element equivalent of this XBankAccount instance
        /// </summary>
        public static explicit operator XElement(XBankAccount account) {
            return account._element;
        }

        /// <summary>
        /// Returns the XBankAccount equivalent of this XElement instance
        /// </summary>
        public static explicit operator XBankAccount(XElement element) {
            if (element.Name == "BankAccount") {
                return new XBankAccount(element);
            } else {
                throw new TypeLoadException("Provided XElement is not a BankAccount");
            }
        }

        #endregion

        /// <summary>
        /// Returns how much of a pov cunt you are
        /// </summary>
        public Money Balance {
            get {
                return _balance;
            }
            set {
                _balance = value;
            }
        }

        /// <summary>
        /// Updates our local balance with the latest from the journal
        /// </summary>
        public void SyncBalance() {
            lock (TransactionJournal.XmlJournal) {
                this.Balance = TransactionJournal.Transactions.Where(i => i.BankAccountFK == this.BankAccountK).Select(i => i.Amount).DefaultIfEmpty(0L).Sum();
            }
        }

        /// <summary>
        /// Asynchronously updates our local balance with the one from the journal
        /// </summary>
        /// <returns></returns>
        public Task SyncBalanceAsync() {
            Guid p = SEconomyPlugin.Profiler.Enter("SyncBalanceAsync");
            
            return Task.Factory.StartNew(() => {
                SyncBalance();
            }).ContinueWith((task) => {
                SEconomyPlugin.Profiler.ExitLog(p);
            });
        }
    }
}

