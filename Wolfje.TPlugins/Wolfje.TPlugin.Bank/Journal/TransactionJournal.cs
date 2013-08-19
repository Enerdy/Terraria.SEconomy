using System;

using System.Collections.Generic;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Linq;
using System.Threading.Tasks;

using System.IO;
using System.IO.Compression;
using System.Xml;

namespace Wolfje.Plugins.SEconomy.Journal {

    /// <summary>
    /// Holds an XML representation of the SEconomy transaction journal.
    /// </summary>
    public sealed partial class TransactionJournal {

        //thread-safety; syncronization object to use when accessing changeable static members
        public static readonly object __staticLock = new object();

        /// <summary>
        /// Returns the current running Xml transaction journal
        /// </summary>
        public static XDocument XmlJournal { get; private set; }

        /// <summary>
        /// Returns the version of the XML schema built into this dll
        /// </summary>
        public static readonly Version XmlSchemaVersion = new Version(1, 2, 0);

        /// <summary>
        /// Makes a deep-copy of the currently running journal and returns the copy.  Changes made on this journal are not going to persist.
        /// </summary>
        public static XDocument JournalCopy {
            get {
                XDocument journalCopy = null;
                lock (__staticLock) {
                    journalCopy = new XDocument(XmlJournal);
                }

                return journalCopy;
            }
        }

       

        /// <summary>
        /// Creates a new, blank transaction journal structure with no accounts or money logs in it.
        /// </summary>
        public TransactionJournal() {}

        /// <summary>
        /// Gets the bank accounts.
        /// </summary>
        public static IEnumerable<XBankAccount> BankAccounts {
            get {
                lock (__staticLock) {
                    return XmlJournal.XPathSelectElements(@"/Journal/BankAccounts/BankAccount").Select(i => (XBankAccount)i);
                }
            }
        }


        /// <summary>
        /// Gets the transaction entries.
        /// </summary>
        public static IEnumerable<XTransaction> Transactions {
            get {
                lock (__staticLock) {
                    return XmlJournal.XPathSelectElements(@"/Journal/Transactions/Transaction").Select(i => (XTransaction)i);
                }
            }
        }

        /// <summary>
        /// Adds an XBankAccount to the bank account collection.
        /// </summary>
        public static XBankAccount AddBankAccount(XBankAccount Account) {
            lock (__staticLock) {
                Account.BankAccountK = RandomString(8);

                XmlJournal.XPathSelectElement("/Journal/BankAccounts").Add((XElement)Account);

                return Account;
            }
        }

        /// <summary>
        /// Adds an XTransaction to the transactions collection.
        /// </summary>
        public static XTransaction AddTransaction(XTransaction Transaction) {
            lock (__staticLock) {
                Transaction.BankAccountTransactionK = RandomString(8);

                XmlJournal.XPathSelectElement("/Journal/Transactions").Add((XElement)Transaction);

                return Transaction;
            }
        }

        /// <summary>
        /// Returns a world account for the current running world.  If it does not exist, one gets created and then returned.
        /// </summary>
        public static XBankAccount EnsureWorldAccountExists() {
            XBankAccount worldAccount = null;

            lock (__staticLock) {
                //World account matches the current world, ignore.
                if (SEconomyPlugin.WorldAccount != null && SEconomyPlugin.WorldAccount.WorldID == Terraria.Main.worldID) {
                    return null;
                }

                if (Terraria.Main.worldID > 0) {


                    worldAccount = (from i in BankAccounts
                                    where (i.Flags & Journal.BankAccountFlags.SystemAccount) == Journal.BankAccountFlags.SystemAccount
                                       && (i.Flags & Journal.BankAccountFlags.PluginAccount) == 0
                                       && i.WorldID == Terraria.Main.worldID
                                    select i).FirstOrDefault();

                    //world account does not exist for this world ID, create one
                    if (worldAccount == null) {
                        //This account is always enabled, locked to the world it's in and a system account (ie. can run into deficit) but not a plugin account
                        XBankAccount newWorldAcc = new XBankAccount("SYSTEM", Terraria.Main.worldID, Journal.BankAccountFlags.Enabled | Journal.BankAccountFlags.LockedToWorld | Journal.BankAccountFlags.SystemAccount, "World account for world " + Terraria.Main.worldName);

                        worldAccount = AddBankAccount(newWorldAcc);
                    }

                    if (worldAccount != null && !string.IsNullOrEmpty(worldAccount.BankAccountK)) {
                        //Is this account listed as enabled?
                        bool accountEnabled = (worldAccount.Flags & Journal.BankAccountFlags.Enabled) == Journal.BankAccountFlags.Enabled;

                        if (!accountEnabled) {
                            TShockAPI.Log.ConsoleError("The world account for world " + Terraria.Main.worldName + " is disabled.  Currency will not work for this game.");
                            return null;
                        }
                    } else {
                        TShockAPI.Log.ConsoleError("There was an error loading the bank account for this world.  Currency will not work for this game.");
                    }
                }
            }

            return worldAccount;
        }


        /// <summary>
        /// Returns an XBankAccount by the UserAccountName
        /// </summary>
        public static XBankAccount GetBankAccountByName(string UserAccountName) {
            lock (__staticLock) {
                return BankAccounts.FirstOrDefault(i => i.UserAccountName == UserAccountName);
            }
        }

        /// <summary>
        /// Returns a bank account by it's key.
        /// </summary>
        public static XBankAccount GetBankAccount(string BankAccountK) {
            lock (__staticLock) {
                Economy.EconomyPlayer matchingAccount = SEconomyPlugin.EconomyPlayers.Where(i => i.BankAccount != null).FirstOrDefault(i => i.BankAccount.BankAccountK != null && i.BankAccount.BankAccountK == BankAccountK);
                //if the player is logged in return the logged in bank account reference
                if (matchingAccount != null) {
                    return matchingAccount.BankAccount;
                } else {
                    lock (XmlJournal) {
                        return BankAccounts.FirstOrDefault(i => i.BankAccountK == BankAccountK);
                    }
                }
            }

        }

        /// <summary>
        /// Deletes a bank account by it's key
        /// </summary>
        public static void DeleteBankAccount(string BankAccountK) {
            lock (__staticLock) {
                XElement account = (XElement)GetBankAccount(BankAccountK);
                account.Remove();
            }
        }

        /// <summary>
        /// Asynchronoyusly deletes all transactions for an account, effectively returning their balance back to 0.
        /// </summary>
        public static Task ResetAccountTransactionsAsync(string BankAccountK) {
            return Task.Factory.StartNew(() => {
                ResetAccountTransactions(BankAccountK);
            });
        }

        /// <summary>
        /// Deletes all transactions for an account, effectively returning their balance back to 0.
        /// </summary>
        public static void ResetAccountTransactions(string BankAccountK) {
            lock (__staticLock) {
                do {
                    XElement trans = XmlJournal.XPathSelectElement(string.Format("/Journal/Transactions/Transaction[@BankAccountFK=\"{0}\"]", BankAccountK));

                    if (trans == null) {
                        break;
                    }

                    var sourceTransactionFK = trans.Attribute("BankAccountTransactionFK").Value;
                    XElement sourceTrans = XmlJournal.XPathSelectElement(string.Format("/Journal/Transactions/Transaction[@BankAccountTransactionK=\"{0}\"]", sourceTransactionFK));
                    trans.Remove();
                    sourceTrans.Remove();

                } while (true);

                XBankAccount account = GetBankAccount(BankAccountK);
                account.SyncBalanceAsync();
            }
        }

        public static XDocument NewJournal() {
            string journalComment = 
@"

This is the SEconomy transaction journal file. 

You have probably guessed by now this is an XML format, this file persists all the transactions and bankaccounts 
in your server instance.  This file is not written to actively, all transaction processing is done in memory and 
coped out to disk every time the backup runs.

Editing this file here isn't going to make your changes persist, once edited you will need to execute /bank loadjournal 
in the server console to resync the in-memory journal with this one.  Be aware that you will lose any in-memory changes 
from now until when the file was writte, this usually results in a minor rollback of people's money.

Obviously it would be retarded to use that command on a journal that is months old.....
";

            string journalAccountComment =
                @"
BankAccounts Collection

This element holds all the bank accounts for a running server. Each BankAccount has a unique account number (starting from 1) and more attributes:

* UserAccountName - The login name of the TShock account this bank account is linked to
* WorldID - The WorldID that the account was created from, this is used when LockedToWorld is set and you want to lock bank accounts to worlds, otherwise they
            are static and are loaded in whichever world you create on the server.
* Flags - A bit-bashed set of flags for the account that control the state of it.  Look in the source for BankAccountFlags for a definition of what the bits do.

Please note, BankAccount elements do not keep a running total of their balance, that is done through summing all Transaction amounts 
(by XPath /Journal/Transactions/Transaction[@BankAccountFK=BankAccountK]/@Amount) linked to this account.
";

            string journalTransComment =
                @"
Transaction Collection

This element holds all the transactions for the current running server.  Each transaction is double-entry accounted, 
which means that a transaction is essentially done twice, representing the loss of money on one account, and the gain 
of money in the destination account or vice-versa.

A double-entry account journal must have two transactions; a source and a destination, and the amounts in each must be 
the inverse of eachother: If money is to be transferred away from a source account the source amount must be negative 
and the destination amount must be positive; and conversely if money is to be transferred into a source account the 
source amount must be postitive and the destination amount must be negative.

A Transaction has these following attributes:

* BankAccountTransactionK - A unique number identifying this transaction
* BankAccountFK - The unique identifier of the BankAccount element this transaction comes from
* Amount - The amount of money this transaction was for; positive for a gain in money, negative for a loss
* Flags - A bit-set flag of transaction options (See source for BankAccountTransferOptions for what they do)
* Flags2 - Unused
* BankAccountTransactionFK - A unique identifier of the opposite side of this double-entry transaction, therefore binding them together.
";

            return new XDocument(new XDeclaration("1.0", "utf-8", "yes"),
                                 new XComment(journalComment),
                                            new XElement("Journal",
                                                new XAttribute("Schema", new Version(1, 2, 0).ToString()),
                                                new XElement("BankAccounts", new XComment(journalAccountComment)),
                                                new XElement("Transactions", new XComment(journalTransComment))
                                            ));
        }

        /// <summary>
        /// Compresses the supplied data and returns a GZipped byte array.
        /// </summary>
        static byte[] GZipCompress(byte[] Data) {
            byte[] compressedData;

            using (MemoryStream outStream = new MemoryStream()) {
                using (GZipStream gzStream = new GZipStream(outStream, CompressionMode.Compress)) {
                    new MemoryStream(Data).CopyTo(gzStream);
                }

                //Copy the compressed stream in its entirety onto the stack
                compressedData = outStream.ToArray();
            }

            return compressedData;
        }

        /// <summary>
        /// Delfates a GZip byte array and returns the uncompressed data.
        /// </summary>
        static byte[] GZipDecompress(byte[] CompressedData) {
            byte[] deflatedData;

            using (MemoryStream outStream = new MemoryStream()) {
                using (GZipStream gzStream = new GZipStream(new MemoryStream(CompressedData), CompressionMode.Decompress)) {
                    gzStream.CopyTo(outStream);
                }

                //Copy the deflated stream in its entirety onto the stack
                deflatedData = outStream.ToArray();
            }

            return deflatedData;
        }

        /// <summary>
        /// Saves the transaction journal to an xml file.
        /// </summary>
        public static void SaveXml(string Path) {
            if (Journal.TransactionJournal.XmlJournal != null) {
                
                //only one write may happen at a time
                lock (__writeLock) {
                    using (MemoryStream ms = new MemoryStream()) {
                        //For some dumbarse reason, XDocument.ToString doesn't render the XDeclaration tag.  The only way to get this is to call Save()
                        //Save only supports a stream or file, we do not want disk I/O in this case
                        JournalCopy.Save(ms);

                        byte[] compressedData = GZipCompress(ms.ToArray());

                        File.WriteAllBytes(Path, compressedData);
                    }
                }
            }
        }

        public static readonly object __writeLock = new object();
        /// <summary>
        /// Asynchronously saves the journal to an XML file.
        /// </summary>
        public static Task SaveXmlAsync(string Path) {

            if (Journal.TransactionJournal.XmlJournal != null) {

                //Only one write may happen at a time.
                lock (__writeLock) {
                    //take a deep-copy of the running journal, so we write the deep copy instead of the running one, saves dumb blocking
                    using (MemoryStream ms = new MemoryStream()) {
                        JournalCopy.Save(ms);
                        byte[] compressedData = GZipCompress(ms.ToArray());

                        //Open the file with create flags, create flags create the file if it doesn't exist, or truncate existing data if it does.
                        //Either way we end up with a clean file to write to.
                        System.IO.FileStream fs = System.IO.File.Open(Path, System.IO.FileMode.Create);

                        //FromAsync takes a Begin/end IAsyncResult pair and applies the TPL model to it into a task, so that we can await it or treat it as an async block
                        return Task.Factory.FromAsync(fs.BeginWrite, fs.EndWrite, compressedData, 0, compressedData.Length, null).ContinueWith((task) => {
                            fs.Close();
                            return task;
                        });
                    }
                }
            } else {
                return null;
            }
        }

        /// <summary>
        /// Loads a journal from file.
        /// </summary>
        public static void LoadFromXmlFile(string Path) {
            TShockAPI.Log.ConsoleInfo("seconomy xml: Loading journal");
            try {
                byte[] fileData = File.ReadAllBytes(Path);
                TShockAPI.Log.ConsoleInfo("seconomy xml: decompressing the journal");
                byte[] uncompressedData = GZipDecompress(fileData);

                TShockAPI.Log.ConsoleInfo(string.Format("seconomy xml: gz {0} Kbytes -> {1} Kbytes", fileData.Length / 1024, uncompressedData.Length / 1024));

                //You can't use XDocument.Parse when you have an XDeclaration for some even dumber reason than the write
                //issue, XDocument has to be constructed from a .net 3.5 XmlReader in this case
                //or you get a parse exception complaining about literal content.
                using (MemoryStream ms = new MemoryStream(uncompressedData)) {
                    using (XmlTextReader xmlStream = new XmlTextReader(ms)) {
                        XmlJournal = XDocument.Load(xmlStream);
                    }
                }

                var accountCount = XmlJournal.XPathEvaluate("count(/Journal/BankAccounts/BankAccount)");
                var tranCount = XmlJournal.XPathEvaluate("count(/Journal/Transactions/Transaction)");

                //Used for XML Schema updates, make sure everyone is running on the same data structure
                ProcessSchema(XmlSchemaVersion);

                TShockAPI.Log.ConsoleInfo(string.Format("seconomy xml: Load clean: {0} accounts and {1} trans.", accountCount, tranCount));
            } catch (Exception ex) {
                if (ex is System.IO.FileNotFoundException || ex is System.IO.DirectoryNotFoundException) {
                    TShockAPI.Log.ConsoleError("seconomy xml: Cannot find file or directory. Creating new one.");

                    XmlJournal = NewJournal();

                    SaveXml(Path);

                } else if (ex is System.Security.SecurityException) {
                    TShockAPI.Log.ConsoleError("seconomy xml: Access denied reading file " + Path);
                } else {
                    TShockAPI.Log.ConsoleError("seconomy xml: error " + ex.ToString());
                }
            }
        }

        private static readonly Random _rng = new Random();
        private const string _chars = "abcdefghijklmnopqrstuvwxyz";
        /// <summary>
        /// Thread-safely generates a random sequence of characters
        /// </summary>
        public static string RandomString(int Size) {
            char[] buffer = new char[Size];

            for (int i = 0; i < Size; i++) {
                int charSeed;
                lock (_rng) {
                    charSeed = _rng.Next(_chars.Length);
                }
                buffer[i] = _chars[charSeed];
            }
            
            return new string(buffer);
        }

        /// <summary>
        /// Processes old XML Schema and incrementally updates them to the current running version
        /// </summary>
        static void ProcessSchema(Version NewVersion) {
            Version runningVersion = null;

            if (XmlJournal.Element("Journal").Attribute("Schema") != null) {
                Version.TryParse(XmlJournal.Element("Journal").Attribute("Schema").Value, out runningVersion);
            }

            if (runningVersion == null && NewVersion == new Version(1, 1, 0)) {
                TShockAPI.Log.ConsoleInfo("seconomy xml: upgrading xml schema to v1.1.0");

                foreach (XElement bankAccount in XmlJournal.XPathSelectElements("/Journal/BankAccounts/BankAccount")) {
                    //assign a new bank account ID
                    string newId = RandomString(8);
                    string oldID = bankAccount.Attribute("BankAccountK").Value;

                    bankAccount.Attribute("BankAccountK").SetValue(newId);

                    foreach (XElement transElement in XmlJournal.XPathSelectElements(string.Format("/Journal/Transactions/Transaction[@BankAccountFK={0}]", oldID))) {
                        transElement.Attribute("BankAccountFK").SetValue(newId);
                    }

                }

                if (XmlJournal.Element("Journal").Attribute("Schema") == null) {
                    XmlJournal.Element("Journal").Add(new XAttribute("Schema", NewVersion.ToString()));
                } else {
                    XmlJournal.Element("Journal").Attribute("Schema").SetValue(NewVersion.ToString());
                }

                SaveXml(Configuration.JournalPath);
            }

            if (runningVersion != null && runningVersion != new Version(1, 2, 0)) {
                TShockAPI.Log.ConsoleInfo("seconomy xml: upgrading xml schema to v1.2.0");
                XmlJournal.Declaration = new XDeclaration("1.0", "utf-8", "yes");

                if (XmlJournal.Element("Journal").Attribute("Schema") == null) {
                    XmlJournal.Element("Journal").Add(new XAttribute("Schema", NewVersion.ToString()));
                } else {
                    XmlJournal.Element("Journal").Attribute("Schema").SetValue(NewVersion.ToString());
                }

                SaveXml(Configuration.JournalPath);
            }

        }

        /// <summary>
        /// Asynchronously writes the journal to a file and logs it.
        /// </summary>
        public static void BackupJournalAsync() {
            TShockAPI.Log.ConsoleInfo("seconomy journal: backing up transaction journal.");
            Guid p = SEconomyPlugin.Profiler.Enter("xml backup");

            Journal.TransactionJournal.SaveXmlAsync(Configuration.JournalPath).ContinueWith((task) => {
                SEconomyPlugin.Profiler.ExitLog(p);
            });
        }

        /// <summary>
        /// Compresses all transactions into one line.  Doing this is going to remove all transaction history but you gain space and processing speed
        /// </summary>
        public static void SquashJournal() {
            int bankAccountCount = BankAccounts.Count();
            int tranCount = Transactions.Count();
            XDocument newJournal = NewJournal();

            bool responsibleForTurningBackupsBackOn = false;

            Console.WriteLine("seconomy xml: beginning Squash");

            if (SEconomyPlugin.BackupCanRun) {
                SEconomyPlugin.BackupCanRun = false;
                responsibleForTurningBackupsBackOn = true;
            }

            for (int i = 0; i < bankAccountCount; i++) {
                XBankAccount account = BankAccounts.ElementAtOrDefault(i);
                if (account != null) {
                    //update account balance
                    account.SyncBalance();

                    string line = string.Format("\r [squash] {0:p} {1}", (double)i / (double)bankAccountCount, account.UserAccountName);
                    SEconomyPlugin.FillWithSpaces(ref line);

                    Console.Write(line);

                    //copy the bank account from the old journal into the new one
                    newJournal.Element("Journal").Element("BankAccounts").Add((XElement)account);

                    //Add the squished summary
                    XTransaction transSummary = new XTransaction(account.Balance);
                    transSummary.BankAccountTransactionK = RandomString(13);
                    transSummary.BankAccountFK = account.BankAccountK;
                    transSummary.Flags = BankAccountTransactionFlags.FundsAvailable | BankAccountTransactionFlags.Squashed;
                    transSummary.Message = "Transaction squash";
                    transSummary.TransactionDateUtc = DateTime.UtcNow;

                    newJournal.Element("Journal").Element("Transactions").Add((XElement)transSummary);
                }
            }

            //abandon the old journal and assign the squashed one
            XmlJournal = newJournal;

            Console.WriteLine("re-syncing online accounts.");

            foreach (XBankAccount account in BankAccounts) {
                XBankAccount runtimeAccount = GetBankAccount(account.BankAccountK);
                if (runtimeAccount != null && runtimeAccount.Owner != null) {
                    Console.WriteLine("re-syncing {0}", runtimeAccount.Owner.TSPlayer.Name);
                    runtimeAccount.SyncBalance();
                }
            }

            SaveXml(Configuration.JournalPath);

            //the backups could already have been disabled by something else.  We don't want to be the ones turning it back on
            if (responsibleForTurningBackupsBackOn) {
                SEconomyPlugin.BackupCanRun = true;
            }


        }

        /// <summary>
        /// Compresses all transactions into one line.  Doing this is going to remove all transaction history but you gain space and processing speed
        /// </summary>
        public static Task SquashJournalAsync() {
            return Task.Factory.StartNew(() => {
                SquashJournal();
            });
        }

    }
}

