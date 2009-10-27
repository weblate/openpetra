﻿/*************************************************************************
 *
 * DO NOT REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
 *
 * @Authors:
 *       timop
 *
 * Copyright 2004-2009 by OM International
 *
 * This file is part of OpenPetra.org.
 *
 * OpenPetra.org is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * OpenPetra.org is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with OpenPetra.org.  If not, see <http://www.gnu.org/licenses/>.
 *
 ************************************************************************/
using System;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using Mono.Unix;
using Ict.Common;
using Ict.Common.IO;
using Ict.Common.Verification;
using Ict.Petra.Client.App.Core.RemoteObjects;
using Ict.Petra.Client.MFinance.Logic;
using Ict.Petra.Shared.MFinance.Account.Data;

namespace Ict.Petra.Client.MFinance.Gui.GL
{
    public partial class TUC_GLBatches
    {
        private Int32 FLedgerNumber;
        private Int32 FSelectedBatchNumber;

        /// <summary>
        /// load the batches into the grid
        /// </summary>
        /// <param name="ALedgerNumber"></param>
        public void LoadBatches(Int32 ALedgerNumber)
        {
            FLedgerNumber = ALedgerNumber;

            // TODO: more criteria: state of batch, period, etc
            FMainDS.Merge(TRemote.MFinance.GL.WebConnectors.LoadABatch(ALedgerNumber));

            ShowData();
        }

        /// <summary>
        /// show ledger number
        /// </summary>
        private void ShowDataManual()
        {
            txtLedgerNumber.Text = TFinanceControls.GetLedgerNumberAndName(FLedgerNumber);
        }

        private void ShowDetailsManual(ABatchRow ARow)
        {
            ((TFrmGLBatch)ParentForm).LoadJournals(
                ARow.LedgerNumber,
                ARow.BatchNumber);
            FSelectedBatchNumber = ARow.BatchNumber;
        }

        private void ShowJournalTab(Object sender, EventArgs e)
        {
            ((TFrmGLBatch)ParentForm).SelectTab(TFrmGLBatch.eGLTabs.Journals);
        }

        /// <summary>
        /// add a new batch
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NewRow(System.Object sender, EventArgs e)
        {
            this.CreateNewABatch();

            // TODO: this.dtpDateCantBeBeyond.Value = AAccountingPeriod[ALedger.CurrentPeriod + ALedger.ForwardingPostingPeriods].EndOfPeriod

            // TODO: on change of FMainDS.ABatch[GetSelectedDetailDataTableIndex()].DateEffective
            // also change FMainDS.ABatch[GetSelectedDetailDataTableIndex()].BatchPeriod
            // and control this.dtpDateCantBeBeyond
        }

        /// <summary>
        /// cancel a batch (there is no deletion of batches)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CancelRow(System.Object sender, EventArgs e)
        {
            // TODO
        }

        private void PostBatch(System.Object sender, EventArgs e)
        {
            // TODO: show VerificationResult
            // TODO: display progress of posting
            TVerificationResultCollection Verifications;

            if (FPetraUtilsObject.HasChanges)
            {
                // save first, then post
                if (!((TFrmGLBatch)ParentForm).SaveChanges())
                {
                    // saving failed, therefore do not try to post
                    MessageBox.Show(Catalog.GetString("The batch was not posted due to problems during saving; ") + Environment.NewLine +
                        Catalog.GetString("Please first save the batch, and then post it!"));
                    return;
                }
            }

            if (!TRemote.MFinance.GL.WebConnectors.PostGLBatch(FLedgerNumber, FSelectedBatchNumber, out Verifications))
            {
                string ErrorMessages = String.Empty;

                foreach (TVerificationResult verif in Verifications)
                {
                    ErrorMessages += "[" + verif.FResultContext + "] " +
                                     verif.FResultTextCaption + ": " +
                                     verif.FResultText + Environment.NewLine;
                }

                System.Windows.Forms.MessageBox.Show(ErrorMessages, Catalog.GetString("Posting failed"));
            }
            else
            {
                // TODO: print reports on successfully posted batch
                MessageBox.Show(Catalog.GetString("The batch has been posted successfully!"));

                // TODO: refresh the grid, to reflect that the batch has been posted
                LoadBatches(FLedgerNumber);
            }
        }

        private void ImportFromSpreadSheet(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();

            dialog.Title = Catalog.GetString("Import transactions from spreadsheet file");
            dialog.Filter = Catalog.GetString("Spreadsheet files (*.csv)|*.csv");

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string directory = Path.GetDirectoryName(dialog.FileName);
                string[] ymlFiles = Directory.GetFiles(directory, "*.yml");
                string definitionFileName = String.Empty;

                if (ymlFiles.Length == 1)
                {
                    definitionFileName = ymlFiles[0];
                }
                else
                {
                    // show another open file dialog for the description file
                    OpenFileDialog dialogDefinitionFile = new OpenFileDialog();
                    dialogDefinitionFile.Title = Catalog.GetString("Please select a yml file that describes the content of the spreadsheet");
                    dialogDefinitionFile.Filter = Catalog.GetString("Data description files (*.yml)|*.yml");

                    if (dialogDefinitionFile.ShowDialog() == DialogResult.OK)
                    {
                        definitionFileName = dialogDefinitionFile.FileName;
                    }
                }

                if (File.Exists(definitionFileName))
                {
                    TYml2Xml parser = new TYml2Xml(definitionFileName);
                    XmlDocument dataDescription = parser.ParseYML2XML();
                    XmlNode RootNode = TXMLParser.FindNodeRecursive(dataDescription.DocumentElement, "RootNode");
                    Int32 FirstTransactionRow = TXMLParser.GetIntAttribute(RootNode, "FirstTransactionRow");
                    string DefaultCostCentre = TXMLParser.GetAttribute(RootNode, "CostCentre");

                    CreateNewABatch();
                    AJournalRow NewJournal = FMainDS.AJournal.NewRowTyped(true);
                    ((TFrmGLBatch)ParentForm).GetJournalsControl().NewRowManual(ref NewJournal);
                    FMainDS.AJournal.Rows.Add(NewJournal);
                    NewJournal.TransactionCurrency = TXMLParser.GetAttribute(RootNode, "Currency");

                    if (Path.GetExtension(dialog.FileName).ToLower() == ".csv")
                    {
                        CreateBatchFromCSVFile(dialog.FileName,
                            RootNode,
                            NewJournal,
                            FirstTransactionRow,
                            DefaultCostCentre);
                    }
                }
            }
        }

        /// load transactions from a CSV file into the currently selected batch
        private void CreateBatchFromCSVFile(string ADataFilename,
            XmlNode ARootNode,
            AJournalRow ARefJournalRow,
            Int32 AFirstTransactionRow,
            string ADefaultCostCentre)
        {
            StreamReader dataFile = new StreamReader(ADataFilename, System.Text.Encoding.Default);

            XmlNode ColumnsNode = TXMLParser.GetChild(ARootNode, "Columns");
            string Separator = TXMLParser.GetAttribute(ARootNode, "Separator");
            string DateFormat = TXMLParser.GetAttribute(ARootNode, "DateFormat");
            string ThousandsSeparator = TXMLParser.GetAttribute(ARootNode, "ThousandsSeparator");
            string DecimalSeparator = TXMLParser.GetAttribute(ARootNode, "DecimalSeparator");

            // read headers
            for (Int32 lineCounter = 0; lineCounter < AFirstTransactionRow - 1; lineCounter++)
            {
                dataFile.ReadLine();
            }

            do
            {
                string line = dataFile.ReadLine();

                ATransactionRow NewTransaction = FMainDS.ATransaction.NewRowTyped(true);
                ((TFrmGLBatch)ParentForm).GetTransactionsControl().NewRowManual(ref NewTransaction, ARefJournalRow);
                FMainDS.ATransaction.Rows.Add(NewTransaction);

                foreach (XmlNode ColumnNode in ColumnsNode.ChildNodes)
                {
                    string Value = StringHelper.GetNextCSV(ref line, Separator);
                    string UseAs = TXMLParser.GetAttribute(ColumnNode, "UseAs");

                    if (UseAs.ToLower() == "reference")
                    {
                        NewTransaction.Reference = Value;
                    }
                    else if (UseAs.ToLower() == "narrative")
                    {
                        NewTransaction.Narrative = Value;
                    }
                    else if (UseAs.ToLower() == "dateeffective")
                    {
                        NewTransaction.TransactionDate = XmlConvert.ToDateTime(Value, DateFormat);
                    }
                    else if (UseAs.ToLower() == "account")
                    {
                        if (Value.Length > 0)
                        {
                            if (Value.Contains(" "))
                            {
                                // cut off currency code; should have been defined in the data description file, for the whole batch
                                Value = Value.Substring(0, Value.IndexOf(" ") - 1);
                            }

                            Value = Value.Replace(ThousandsSeparator, "");
                            Value = Value.Replace(DecimalSeparator, ".");

                            NewTransaction.TransactionAmount = Convert.ToDouble(Value, System.Globalization.CultureInfo.InvariantCulture);
                            NewTransaction.CostCentreCode = ADefaultCostCentre;
                            NewTransaction.AccountCode = ColumnNode.Name;
                        }
                    }
                }
            } while (!dataFile.EndOfStream);
        }
    }
}