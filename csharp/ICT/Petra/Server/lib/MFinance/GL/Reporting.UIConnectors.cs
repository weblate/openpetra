//
// DO NOT REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
//
// @Authors:
//       timop
//
// Copyright 2004-2012 by OM International
//
// This file is part of OpenPetra.org.
//
// OpenPetra.org is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// OpenPetra.org is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with OpenPetra.org.  If not, see <http://www.gnu.org/licenses/>.
//
using System;
using System.Data;
using System.Collections.Generic;
using Ict.Petra.Shared;
using Ict.Common;
using Ict.Common.Data;
using Ict.Common.DB;
using Ict.Common.Remoting.Server;
using Ict.Common.Remoting.Shared;
using Ict.Petra.Server.MFinance.Cacheable;
using Ict.Petra.Shared.MFinance;
using Ict.Petra.Shared.MFinance.GL.Data;
using Ict.Petra.Shared.MFinance.Account.Data;
using Ict.Petra.Shared.MPartner.Partner.Data;
using Ict.Petra.Shared.MReporting;
using Ict.Petra.Shared.Interfaces.MFinance;
using Ict.Petra.Server.App.Core.Security;
using Ict.Petra.Server.MFinance.Setup.WebConnectors;
using System.Collections;

namespace Ict.Petra.Server.MFinance.Reporting.WebConnectors
{
    ///<summary>
    /// This WebConnector provides data for the finance reporting screens
    ///</summary>
    public class TFinanceReportingWebConnector
    {
        /// <summary>
        /// get the details of the given ledger
        /// </summary>
        [RequireModulePermission("FINANCE-1")]
        public static void GetLedgerPeriodDetails(
            int ALedgerNumber,
            out int ANumberAccountingPeriods,
            out int ANumberForwardingPeriods,
            out int ACurrentPeriod,
            out int ACurrentYear)
        {
            System.Type typeofTable = null;
            TCacheable CachePopulator = new TCacheable();
            ALedgerTable CachedDataTable = (ALedgerTable)CachePopulator.GetCacheableTable(
                TCacheableFinanceTablesEnum.LedgerDetails,
                "",
                false,
                ALedgerNumber,
                out typeofTable);

            if (CachedDataTable.Rows.Count > 0)
            {
                ANumberAccountingPeriods = CachedDataTable[0].NumberOfAccountingPeriods;
                ANumberForwardingPeriods = CachedDataTable[0].NumberFwdPostingPeriods;
                ACurrentPeriod = CachedDataTable[0].CurrentPeriod;
                ACurrentYear = CachedDataTable[0].CurrentFinancialYear;
            }
            else
            {
                ANumberAccountingPeriods = -1;
                ANumberForwardingPeriods = -1;
                ACurrentPeriod = -1;
                ACurrentYear = -1;
            }
        }

        /// <summary>
        /// Loads all available financial years into a table
        /// To be used by a combobox to select the financial year
        ///
        /// </summary>
        /// <returns>DataTable</returns>
        [RequireModulePermission("FINANCE-1")]
        public static DataTable GetAvailableFinancialYears(int ALedgerNumber,
            System.Int32 ADiffPeriod,
            out String ADisplayMember,
            out String AValueMember)
        {
            return Ict.Petra.Server.MFinance.GL.WebConnectors.TAccountingPeriodsWebConnector.GetAvailableGLYears(
                ALedgerNumber,
                ADiffPeriod,
                false,
                out ADisplayMember,
                out AValueMember);
        }

        /// <summary>
        /// Load all the receiving fields
        /// </summary>
        /// <returns>Table with the field keys and the field names</returns>
        [RequireModulePermission("FINANCE-1")]
        public static DataTable GetReceivingFields(int ALedgerNumber, out String ADisplayMember, out String AValueMember)
        {
            DataTable ReturnTable = new DataTable();
            String sql;

            TDBTransaction ReadTransaction;

            ADisplayMember = "FieldName";
            AValueMember = "FieldKey";

            ReadTransaction = DBAccess.GDBAccessObj.BeginTransaction();
            try
            {
                sql = "SELECT DISTINCT " + PPartnerTable.GetTableDBName() + "." + PPartnerTable.GetPartnerKeyDBName() + " AS " + AValueMember +
                      ", " +
                      PPartnerTable.GetTableDBName() + "." + PPartnerTable.GetPartnerShortNameDBName() + " AS " + ADisplayMember +
                      " FROM " + PPartnerTable.GetTableDBName() + ", " +
                      PPartnerTypeTable.GetTableDBName() +
                      " WHERE " +
                      PPartnerTypeTable.GetTableDBName() + "." + PPartnerTypeTable.GetPartnerKeyDBName() + " = " + PPartnerTable.GetTableDBName() +
                      "." + PPartnerTable.GetPartnerKeyDBName() +
                      " AND (" + PPartnerTypeTable.GetTableDBName() + "." + PPartnerTypeTable.GetTypeCodeDBName() + " = 'LEDGER' OR " +
                      PPartnerTypeTable.GetTableDBName() + "." + PPartnerTypeTable.GetTypeCodeDBName() + " = 'COSTCENTRE' " +
                      ") ORDER BY " + PPartnerTable.GetTableDBName() + "." + PPartnerTable.GetPartnerShortNameDBName();

                ReturnTable = DBAccess.GDBAccessObj.SelectDT(sql, "BatchYearTable", ReadTransaction);
            }
            finally
            {
                DBAccess.GDBAccessObj.RollbackTransaction();
            }
            return ReturnTable;
        }

        private static void GetReportingCostCentres(ACostCentreTable ACostCentres, ref List <string>AResult, string ASummaryCostCentreCode)
        {
            if (ASummaryCostCentreCode.Length == 0)
            {
                return;
            }

            string[] CostCentres = ASummaryCostCentreCode.Split(new char[] { ',' });

            foreach (string costcentre in CostCentres)
            {
                DataRowView[] ReportingCostCentres = ACostCentres.DefaultView.FindRows(costcentre);

                if (ReportingCostCentres.Length > 0)
                {
                    foreach (DataRowView rv in ReportingCostCentres)
                    {
                        ACostCentreRow row = (ACostCentreRow)rv.Row;

                        if (row.PostingCostCentreFlag)
                        {
                            AResult.Add(row.CostCentreCode);
                        }
                        else
                        {
                            GetReportingCostCentres(ACostCentres, ref AResult, row.CostCentreCode);
                        }
                    }
                }
                else
                {
                    DataView dv = new DataView(ACostCentres);
                    dv.Sort = ACostCentreTable.GetCostCentreCodeDBName();
                    ACostCentreRow cc = (ACostCentreRow)dv.FindRows(costcentre)[0].Row;

                    if (cc.PostingCostCentreFlag)
                    {
                        AResult.Add(costcentre);
                    }
                }
            }
        }

        /// <summary>
        /// Get all cost centres that report into the given summary cost centre
        /// </summary>
        /// <returns>a CSV list of the reporting cost centres</returns>
        [RequireModulePermission("FINANCE-1")]
        public static string GetReportingCostCentres(int ALedgerNumber, String ASummaryCostCentreCode, string ARemoveCostCentresFromList)
        {
            System.Type typeofTable = null;
            TCacheable CachePopulator = new TCacheable();
            ACostCentreTable CachedDataTable = (ACostCentreTable)CachePopulator.GetCacheableTable(
                TCacheableFinanceTablesEnum.CostCentreList,
                "",
                false,
                ALedgerNumber,
                out typeofTable);

            CachedDataTable.DefaultView.Sort = ACostCentreTable.GetCostCentreToReportToDBName();

            List <string>Result = new List <string>();

            GetReportingCostCentres(CachedDataTable, ref Result, ASummaryCostCentreCode);

            List <string>IgnoreCostCentres = new List <string>();

            GetReportingCostCentres(CachedDataTable, ref IgnoreCostCentres, ARemoveCostCentresFromList);

            foreach (string s in IgnoreCostCentres)
            {
                if (Result.Contains(s))
                {
                    Result.Remove(s);
                }
            }

            return StringHelper.StrMerge(Result.ToArray(), ',');
        }

        private static void GetReportingAccounts(AAccountHierarchyDetailTable AAccountHierarchyDetail,
            ref List <string>AResult,
            string ASummaryAccountCodes,
            string AAccountHierarchy)
        {
            string[] Accounts = ASummaryAccountCodes.Split(new char[] { ',' });

            foreach (string account in Accounts)
            {
                DataRowView[] ReportingAccounts = AAccountHierarchyDetail.DefaultView.FindRows(new object[] { AAccountHierarchy, account });

                if (ReportingAccounts.Length == 0)
                {
                    AResult.Add(account);
                }
                else
                {
                    foreach (DataRowView rv in ReportingAccounts)
                    {
                        AAccountHierarchyDetailRow row = (AAccountHierarchyDetailRow)rv.Row;

                        GetReportingAccounts(AAccountHierarchyDetail, ref AResult, row.ReportingAccountCode, AAccountHierarchy);
                    }
                }
            }
        }

        /// <summary>
        /// Get all accounts that report into the given summary account
        /// </summary>
        /// <returns>a CSV list of the reporting accounts</returns>
        [RequireModulePermission("FINANCE-1")]
        public static string GetReportingAccounts(int ALedgerNumber, string ASummaryAccountCodes, string ARemoveAccountsFromList)
        {
            GLSetupTDS MainDS = TGLSetupWebConnector.LoadAccountHierarchies(ALedgerNumber);

            List <string>accountcodes = new List <string>();

            MainDS.AAccountHierarchyDetail.DefaultView.Sort =
                AAccountHierarchyDetailTable.GetAccountHierarchyCodeDBName() + "," +
                AAccountHierarchyDetailTable.GetAccountCodeToReportToDBName();


            GetReportingAccounts(MainDS.AAccountHierarchyDetail, ref accountcodes, ASummaryAccountCodes, MFinanceConstants.ACCOUNT_HIERARCHY_STANDARD);

            string[] RemoveAccountsFromList = ARemoveAccountsFromList.Split(new char[] { ',' });

            foreach (string s in RemoveAccountsFromList)
            {
                if (accountcodes.Contains(s))
                {
                    accountcodes.Remove(s);
                }
            }

            return StringHelper.StrMerge(accountcodes.ToArray(), ',');
        }

        /// <summary>
        /// Get the name for this Ledger
        /// </summary>
        [RequireModulePermission("FINANCE-1")]
        public static string GetLedgerName(int ledgernumber)
        {
            String ReturnValue = "";
            String strSql = "SELECT p_partner_short_name_c FROM PUB_a_ledger, PUB_p_partner WHERE a_ledger_number_i=" +
                            StringHelper.IntToStr(ledgernumber) + " and PUB_a_ledger.p_partner_key_n = PUB_p_partner.p_partner_key_n";
            DataTable tab = DBAccess.GDBAccessObj.SelectDT(strSql, "GetLedgerName_TempTable", null);

            if (tab.Rows.Count > 0)
            {
                ReturnValue = Convert.ToString(tab.Rows[0]["p_partner_short_name_c"]);
            }

            return ReturnValue;
        }

        /// <summary>
        /// Returns a DataSet to the client for use in client-side reporting
        /// </summary>
        [RequireModulePermission("FINANCE-1")]
        public static GLReportingTDS GetReportingDataSet(String ADataSetFilterCsv)
        {
            GLReportingTDS MainDs = new GLReportingTDS();
            TDBTransaction Transaction = DBAccess.GDBAccessObj.BeginTransaction();

            while (ADataSetFilterCsv != "")
            {
                String Tbl = StringHelper.GetNextCSV(ref ADataSetFilterCsv, ",", "");
                String[] part = Tbl.Split('/');
                String OrderBy = "";

                if (part.Length > 4)
                {
                    OrderBy = part[4];
                }

                String Query = "SELECT " + part[1] + " FROM " + part[2] + " WHERE " + part[3] + OrderBy;
                MainDs.Tables[part[0]].Merge(DBAccess.GDBAccessObj.SelectDT(Query, part[0], Transaction));
            }

            DBAccess.GDBAccessObj.RollbackTransaction();
            return MainDs;
        }

        /// <summary>
        /// Collects the period opening / closing balances for the Account Detail report
        /// </summary>
        /// <param name="ALedgerFilter"></param>
        /// <param name="AAccountCodeFilter"></param>
        /// <param name="ACostCentreFilter"></param>
        /// <param name="AStartPeriod"></param>
        /// <param name="AEndPeriod"></param>
        /// <param name="AInternational"></param>
        /// <returns>DataTable</returns>
        [RequireModulePermission("FINANCE-1")]
        public static DataTable GetPeriodBalances(String ALedgerFilter,
                    String AAccountCodeFilter,
                    String ACostCentreFilter,
                    Int32 AStartPeriod,
                    Int32 AEndPeriod,
                    Boolean AInternational)
        {
            Boolean FromStartOfYear = (AStartPeriod == 1);
            if (!FromStartOfYear)
            {
                AStartPeriod -= 1; // I want the closing balance of the previous period.
            }
            String Query = "SELECT * FROM a_ledger WHERE " + ALedgerFilter;
            DataTable LedgerTable = DBAccess.GDBAccessObj.SelectDT(Query, "Ledger", null);
            Int32 FiancialYear = Convert.ToInt32(LedgerTable.Rows[0]["a_current_financial_year_i"]);

            String BalanceField = (AInternational) ? "glmp.a_actual_intl_n" : "glmp.a_actual_base_n";
            String StartBalanceField = (AInternational) ? "glm.a_start_balance_intl_n" : "glm.a_start_balance_base_n";

            Query = "SELECT glm.a_cost_centre_code_c, glm.a_account_code_c, glmp.a_period_number_i, "
                + StartBalanceField + " AS start_balance, "
                + BalanceField + " AS balance "
                + " FROM a_general_ledger_master AS glm, a_general_ledger_master_period AS glmp"
                + " WHERE glm." + ALedgerFilter
                + " AND glm.a_year_i = " + FiancialYear
                + AAccountCodeFilter
                + ACostCentreFilter
                + " AND glm.a_glm_sequence_i = glmp.a_glm_sequence_i"
                + " AND glmp.a_period_number_i >= " + AStartPeriod
                + " AND glmp.a_period_number_i <= " + AEndPeriod
                + " ORDER BY glm.a_cost_centre_code_c, glm.a_account_code_c, glmp.a_period_number_i";
            DataTable GlmTbl = DBAccess.GDBAccessObj.SelectDT(Query, "balances", null);
            DataTable Results = new DataTable();
            Results.Columns.Add(new DataColumn("a_cost_centre_code_c", typeof(string)));
            Results.Columns.Add(new DataColumn("a_account_code_c", typeof(string)));
            Results.Columns.Add(new DataColumn("OpeningBalance", typeof(Decimal)));
            Results.Columns.Add(new DataColumn("ClosingBalance", typeof(Decimal)));

            String CostCentre = "";
            String AccountCode = "";
            Decimal OpeningBalance = 0;
            Decimal ClosingBalance = 0;
            Int32 MaxPeriod = -1;
            Int32 MinPeriod = 99;

            // For each CostCentre / Account combination  I want just a single row, with the opening and closing balances,
            // so I need to pre-process the stuff I've got in this table, and generate another table.

            foreach (DataRow row in GlmTbl.Rows)
            {
                if ((row["a_cost_centre_code_c"].ToString() != CostCentre) || (row["a_account_code_c"].ToString() != AccountCode)) // a new CC/AC combination
                {
                    if (CostCentre != "" && AccountCode != "") // Add a new row, but not if there's no data yet.
                    {
                        DataRow NewRow = Results.NewRow();
                        NewRow["a_cost_centre_code_c"] = CostCentre;
                        NewRow["a_account_code_c"] = AccountCode;
                        NewRow["OpeningBalance"] = OpeningBalance;
                        NewRow["ClosingBalance"] = ClosingBalance;
                        Results.Rows.Add(NewRow);
                    }

                    CostCentre = row["a_cost_centre_code_c"].ToString();
                    AccountCode = row["a_account_code_c"].ToString();
                    MaxPeriod = -1;
                    MinPeriod = 99;
                }

                Int32 ThisPeriod = Convert.ToInt32(row["a_period_number_i"]);

                if (ThisPeriod < MinPeriod)
                {
                    MinPeriod = ThisPeriod;
                    OpeningBalance = (FromStartOfYear) ? Convert.ToDecimal(row["start_balance"]) : Convert.ToDecimal(row["balance"]);
                }

                if (ThisPeriod > MaxPeriod)
                {
                    MaxPeriod = ThisPeriod;
                    ClosingBalance = Convert.ToDecimal(row["balance"]);
                }
            }

            return Results;
        }

        //
        // Find the level of nesting for this account, using recursive call
        private static Int32 GetAccountLevel(DataView HierarchyView, String AccountCode, ref String AccountPath, Int32 ChildLevel)
        {
            Int32 RowNum = HierarchyView.Find(AccountCode);
            if (RowNum < 0)
            {
                return ChildLevel;
            }
            else
            {
                DataRow Row = HierarchyView[RowNum].Row;
                AccountPath = Row["AccountCode"].ToString() + "/" + AccountPath;
                return GetAccountLevel(HierarchyView, Row["ReportsTo"].ToString(), ref AccountPath, ChildLevel + 1);
            }
        }

        /// <summary>
        /// Returns a DataSet to the client for use in client-side reporting
        /// </summary>
        [RequireModulePermission("FINANCE-1")]
        public static DataTable IncomeExpenseTable(Dictionary<String, TVariant> AParameters)
        {
            /* Required columns:
             *   CostCentreCode
             *   CostCentreName
             *   AccountType
             *   AccountLevel
             *   AccountPath
             *   AccountCode
             *   AccountName
             *   YearStart
             *   Actual
             *   ActualYTD
             *   ActualLastYear
             *   ActualLastYearComplete
             *   Budget
             *   BudgetYTD
             *   BudgetLastYear
             *   BudgetWholeYear
             */


            Int32 LedgerNumber   = AParameters["param_ledger_number_i"].ToInt32();
            Int32 AccountingYear = AParameters["param_year_i"].ToInt32();
            Int32 ReportPeriod   = AParameters["param_start_period_i"].ToInt32();
            String HierarchyName = AParameters["param_account_hierarchy_c"].ToString();

            //
            // Read different DB fields according to currency setting
            String ActualFieldName =  AParameters["param_currency"].ToString().StartsWith("Int")? "a_actual_intl_n" : "a_actual_base_n";
            String BudgetFieldName =  AParameters["param_currency"].ToString().StartsWith("Int")? "a_budget_intl_n" : "a_budget_base_n";

            String CostCentreFilter = "";
            String CostCentreOptions = AParameters["param_costcentreoptions"].ToString();
            if (CostCentreOptions == "SelectedCostCentres")
            {
                String CostCentreList = AParameters["param_cost_centre_codes"].ToString();
                CostCentreList = CostCentreList.Replace(",", "','");                             // SQL IN List items in single quotes
                CostCentreFilter = " AND glm.a_cost_centre_code_c in ('" + CostCentreList + "')";
            }
            if (CostCentreOptions == "AllActiveCostCentres")
            {
                CostCentreFilter = " AND a_cost_centre.a_cost_centre_active_flag_l=true";
            }

            // To find the Budget YTD, I need to sum all the budget fields from the start of the year.

            String BudgetYtdQuery = "(CASE WHEN glm.a_year_i=" + AccountingYear
                + " AND a_period_number_i=" + ReportPeriod
                + " THEN (SELECT SUM(" + BudgetFieldName + ") FROM a_general_ledger_master_period"
                + " WHERE a_glm_sequence_i= glm.a_glm_sequence_i AND a_period_number_i <= " + ReportPeriod 
                + " ) ELSE 0 END)";

            String Query = "SELECT DISTINCT"
                + " 1 AS AccountLevel,"
                + " false AS HasChildren,"
                + " false AS ParentFooter,"
                + " glm.a_account_code_c AS AccountPath,"
                + " glm.a_glm_sequence_i AS seq,"
                + " glm.a_year_i AS Year,"
                + " glmp.a_period_number_i AS Period,"
                + " glm.a_cost_centre_code_c AS CostCentreCode,"
                + " a_cost_centre.a_cost_centre_name_c AS CostCentreName,"
                + " a_account.a_account_type_c AS AccountType,"
                + " glm.a_account_code_c AS AccountCode,"
                + " a_account.a_account_code_short_desc_c AS AccountName,"
                + " glm.a_start_balance_base_n AS YearStart,"
                + " 0.1 AS Actual,"
                + " glmp." + ActualFieldName + " AS ActualYTD,"
                + " 0.1 AS ActualLastYear,"
                + " glmp." + BudgetFieldName + " AS Budget,"
                + " " + BudgetYtdQuery + " AS BudgetYTD,"
                + " 0.1 AS BudgetLastYear,"
                + " 0.1 AS WholeYearBudget"

                + " FROM a_general_ledger_master AS glm, a_general_ledger_master_period AS glmp, a_account, a_cost_centre"
                + " WHERE glm.a_ledger_number_i=" + LedgerNumber
                + " AND glm.a_year_i>=" + (AccountingYear-1)
                + " AND glm.a_year_i<=" + AccountingYear
                + " AND glm.a_glm_sequence_i = glmp.a_glm_sequence_i"
                + " AND glmp.a_period_number_i>=" + (ReportPeriod-1)
                + " AND glmp.a_period_number_i<=" + ReportPeriod
                + " AND a_account.a_account_code_c = glm.a_account_code_c"
                + " AND (a_account.a_account_type_c = 'Income' OR a_account.a_account_type_c = 'Expense')"
                + " AND a_account.a_ledger_number_i = glm.a_ledger_number_i"
                + " AND a_cost_centre.a_ledger_number_i = glm.a_ledger_number_i"
                + " AND a_cost_centre.a_cost_centre_code_c = glm.a_cost_centre_code_c"
                + CostCentreFilter
                + " ORDER BY glm.a_cost_centre_code_c"
                ;
            DataTable resultTable = DBAccess.GDBAccessObj.SelectDT(Query, "IncomeExpense", null);

            //
            // The table includes YTD balances, but I need the balance for the specified period.

            DataView OldPeriod = new DataView(resultTable);
            DataView ThisMonth = new DataView(resultTable);
            ThisMonth.RowFilter = "Period=" + ReportPeriod;

            //
            // If I have rows for the previous month too, I can subtract the previous month's YTD balance..
            if (ReportPeriod > 1)
            {
                foreach (DataRowView rv in ThisMonth)
                {
                    DataRow Row = rv.Row;
                    OldPeriod.RowFilter = String.Format("Year={0} AND Period={1} AND CostCentreCode='{2}' AND AccountCode='{3}'",
                        Convert.ToInt32(Row["Year"]),
                        ReportPeriod - 1,
                        Row["CostCentreCode"].ToString(),
                        Row["AccountCode"].ToString()
                        );
                    DataRow LastMonthRow = OldPeriod[0].Row;
                    Row["Actual"] = Convert.ToDecimal(Row["ActualYTD"]) - Convert.ToDecimal(LastMonthRow["ActualYTD"]);
                }
            }
            else
            {
                //
                // For the first period of the year, I can just subtract the YearStart balance, which I already have.
                foreach (DataRowView rv in ThisMonth)
                {
                    DataRow Row = rv.Row;
                    Row["Actual"] = Convert.ToDecimal(Row["ActualYTD"]) - Convert.ToDecimal(Row["YearStart"]);
                }
            }

            //
            // Some of these rows are from a year ago. I've updated their "Actual" values;
            // now I'll copy those into the current period "LastYear" fields.
            foreach (DataRowView rv in ThisMonth)
            {
                DataRow Row = rv.Row;
                OldPeriod.RowFilter = String.Format("Year={0} AND Period={1} AND CostCentreCode='{2}' AND AccountCode='{3}'",
                    AccountingYear - 1,
                    ReportPeriod,
                    Row["CostCentreCode"].ToString(),
                    Row["AccountCode"].ToString()
                    );
                if (OldPeriod.Count > 0)
                {
                    DataRow LastYearRow = OldPeriod[0].Row;
                    Row["ActualLastYear"] = Convert.ToDecimal(LastYearRow["Actual"]);
                    Row["BudgetLastYear"] = Convert.ToDecimal(LastYearRow["Budget"]);
                }
            }

            //
            // I want to show what "Level" the accounts are at - that is, how far down the heirarchy they are.
            // At the same time I'll construct the "AccountPath" field, and use it for sorting.

            Query = "SELECT a_reporting_account_code_c AS AccountCode, a_account_code_to_report_to_c AS ReportsTo"
                + " FROM a_account_hierarchy_detail WHERE a_ledger_number_i=" + LedgerNumber
                + " AND a_account_hierarchy_code_c='" + HierarchyName + "' ORDER BY AccountCode";
            DataTable HeirarchyTbl = DBAccess.GDBAccessObj.SelectDT(Query, "IncomeExpense", null);
            DataView HeirarchyView = new DataView(HeirarchyTbl);
            HeirarchyView.Sort = "AccountCode";

            ThisMonth.RowFilter = "Year=" + AccountingYear + " AND Period=" + ReportPeriod  // Only current period
                + " AND (Actual <> 0 OR ActualYTD <> 0 OR Budget <> 0 OR BudgetYTD <> 0)";  // Only non-zero rows
                                                                                            // - don't calculate the other rows because I'm not returning them!

            foreach (DataRowView rv in ThisMonth)
            {
                DataRow Row = rv.Row;
                String AccountPath = "";
                Row["AccountLevel"] = GetAccountLevel(HeirarchyView, Row["AccountCode"].ToString(), ref AccountPath, -3);   // The negative offset here causes my
                Row["AccountPath"] = AccountPath;                                                                           // "Root" account to have level 0.
            }

            //
            // Now if I re-order the result by AccountPath, hide all the old data and empty rows, and rows that are too detailed, it should be what I need!

            String DepthFilter = "";
            String DepthOption = AParameters["param_depth"].ToString();
            if (DepthOption == "summary")
            {
                DepthFilter = " AND AccountLevel<=1";
            }
            if (DepthOption == "standard")
            {
                DepthFilter = " AND AccountLevel<=3";
            }

            ThisMonth.Sort = "CostCentreCode, AccountType DESC, AccountPath ASC";

            ThisMonth.RowFilter = "Year=" + AccountingYear + " AND Period=" + ReportPeriod  // Only current period
                + " AND (Actual <> 0 OR ActualYTD <> 0 OR Budget <> 0 OR BudgetYTD <> 0)"   // Only non-zero rows
                + DepthFilter;                                                              // Nothing too detailed

            DataTable FilteredResults = ThisMonth.ToTable("IncomeExpense");

            //
            // I need to add in the "whole year budget" field:
            foreach (DataRow Row in FilteredResults.Rows)
            {
                Query = "SELECT SUM(" + BudgetFieldName + ") AS WholeYearBudget FROM a_general_ledger_master_period WHERE a_glm_sequence_i="
                    + Convert.ToInt32(Row["Seq"]);
                DataTable YearBudgetTbl = DBAccess.GDBAccessObj.SelectDT(Query, "YearBudget", null);
                if (YearBudgetTbl.Rows.Count > 0)
                {
                    Row["WholeYearBudget"] = YearBudgetTbl.Rows[0]["WholeYearBudget"];
                }
            }


            //
            // Finally, to make the hierarchical report possible,
            // I want to include a note to show whether a row has child rows,
            // and if it does, I'll copy this row to a new "footer" row, below the children.
            for (Int32 RowIdx = 0; RowIdx < FilteredResults.Rows.Count-1; RowIdx++)
            {
                Int32 ParentAccountLevel = Convert.ToInt32(FilteredResults.Rows[RowIdx]["AccountLevel"]);
                Boolean HasChildren = (Convert.ToInt32(FilteredResults.Rows[RowIdx + 1]["AccountLevel"]) > ParentAccountLevel);
                FilteredResults.Rows[RowIdx]["HasChildren"] = HasChildren;
                if (HasChildren)
                {
                    Int32 NextSiblingPos = -1;
                    for (Int32 ChildIdx = RowIdx + 2; ChildIdx < FilteredResults.Rows.Count; ChildIdx++)
                    {
                        if (Convert.ToInt32(FilteredResults.Rows[ChildIdx]["AccountLevel"]) <= ParentAccountLevel)  // This row is not a child of mine
                        {                                                                                           // so I insert my footer before here.
                            NextSiblingPos = ChildIdx;
                            break;
                        }
                    }
                    DataRow FooterRow = FilteredResults.NewRow();
                    DataUtilities.CopyAllColumnValues(FilteredResults.Rows[RowIdx], FooterRow);
                    FooterRow["ParentFooter"] = true;
                    FooterRow["HasChildren"] = false;
                    if (NextSiblingPos > 0)
                    {
                        FilteredResults.Rows.InsertAt(FooterRow, NextSiblingPos);
                    }
                    else
                    {
                        FilteredResults.Rows.Add(FooterRow);
                    }
                }
            }
            return FilteredResults;
        }


        /// <summary>
        /// Returns a DataSet to the client for use in client-side reporting
        /// </summary>
        [RequireModulePermission("FINANCE-1")]
        public static DataTable HosaGiftsTable(Dictionary <String, TVariant>AParameters)
        {
            Boolean NewTransaction = false;

            try
            {
                Boolean PersonalHosa = (AParameters["param_filter_cost_centres"].ToString() == "PersonalCostcentres");
                Int32 LedgerNumber = AParameters["param_ledger_number_i"].ToInt32();
                String CostCentreCodes = AParameters["param_cost_centre_codes"].ToString();
                Int32 IchNumber = AParameters["param_ich_number"].ToInt32();

                String DateFilter = "";

                if (AParameters["param_period"].ToBool() == true)
                {
                    Int32 periodYear = AParameters["param_year_i"].ToInt32();
                    Int32 periodStart = AParameters["param_start_period_i"].ToInt32();
                    Int32 periodEnd = AParameters["param_end_period_i"].ToInt32();
                    DateFilter = "AND GiftBatch.a_batch_year_i = " + periodYear;

                    if (periodStart == periodEnd)
                    {
                        DateFilter += (" AND GiftBatch.a_batch_period_i = " + periodStart + " ");
                    }
                    else
                    {
                        DateFilter += (" AND GiftBatch.a_batch_period_i >= " + periodStart +
                                       " AND GiftBatch.a_batch_period_i <= " + periodEnd + " ");
                    }
                }
                else
                {
                    DateTime dateStart = AParameters["param_start_date"].ToDate();
                    DateTime dateEnd = AParameters["param_end_date"].ToDate();
                    DateFilter = "AND GiftBatch.a_gl_effective_date_d >= " + dateStart.ToString("yyyy-MM-dd") +
                                 " AND GiftBatch.a_gl_effective_date_d <= " + dateEnd.ToString("yyyy-MM-dd") + " ";
                }

                String Query = "SELECT ";

                if (PersonalHosa)
                {
                    Query += "LinkedCostCentre.a_cost_centre_code_c AS CostCentre, ";
                }
                else
                {
                    Query += "GiftDetail.a_cost_centre_code_c AS CostCentre, ";
                }

                Query +=
                    "MotivationDetail.a_account_code_c AS AccountCode, SUM(GiftDetail.a_gift_amount_n) AS GiftBaseAmount, SUM(a_gift_transaction_amount_n) AS GiftTransactionAmount, "
                    +
                    "GiftDetail.p_recipient_key_n AS RecipientKey, Partner.p_partner_short_name_c AS RecipientShortname, " +
                    "Partner.p_partner_short_name_c AS Narrative " +
                    "FROM a_gift_detail AS GiftDetail, a_gift_batch AS GiftBatch, " +
                    "a_motivation_detail AS MotivationDetail, a_gift AS Gift, p_partner AS Partner";

                if (PersonalHosa)
                {
                    Query += ",PUB_a_valid_ledger_number AS LinkedCostCentre";
                }

                Query += " WHERE GiftDetail.a_ledger_number_i = GiftBatch.a_ledger_number_i " +
                         "AND GiftDetail.a_batch_number_i = GiftBatch.a_batch_number_i " +
                         "AND GiftDetail.a_ledger_number_i = MotivationDetail.a_ledger_number_i " +
                         "AND GiftDetail.a_motivation_group_code_c = MotivationDetail.a_motivation_group_code_c " +
                         "AND GiftDetail.a_motivation_detail_code_c = MotivationDetail.a_motivation_detail_code_c " +
                         "AND GiftDetail.a_ledger_number_i = Gift.a_ledger_number_i " +
                         "AND GiftDetail.a_batch_number_i = Gift.a_batch_number_i " +
                         "AND GiftDetail.a_gift_transaction_number_i = Gift.a_gift_transaction_number_i " +
                         "AND Partner.p_partner_key_n = GiftDetail.p_recipient_key_n " +
                         "AND GiftDetail.a_ledger_number_i = " + LedgerNumber + " " +
                         "AND GiftBatch.a_batch_status_c = '" + MFinanceConstants.BATCH_POSTED + "' " +
                         DateFilter;

                if (PersonalHosa)
                {
                    Query += "AND LinkedCostCentre.a_ledger_number_i = GiftDetail.a_ledger_number_i " +
                             "AND LinkedCostCentre.a_cost_centre_code_c IN (" + CostCentreCodes + ") " +
                             "AND GiftDetail.p_recipient_key_n = LinkedCostCentre.p_partner_key_n ";
                }
                else
                {
                    Query += "AND GiftDetail.a_cost_centre_code_c IN (" + CostCentreCodes + ") ";
                }

                if (IchNumber != 0)
                {
                    Query += "AND GiftDetail.a_ich_number_i = " + IchNumber + " ";
                }

                Query += "GROUP BY CostCentre, AccountCode, GiftDetail.p_recipient_key_n, Partner.p_partner_short_name_c " +
                         "ORDER BY Partner.p_partner_short_name_c ASC";

                TDBTransaction Transaction = DBAccess.GDBAccessObj.GetNewOrExistingTransaction(IsolationLevel.ReadCommitted, out NewTransaction);
                DataTable resultTable = DBAccess.GDBAccessObj.SelectDT(Query, "Gifts", Transaction);

                resultTable.Columns.Add("GiftIntlAmount", typeof(Decimal));
                resultTable.Columns.Add("Reference", typeof(string));

                Boolean InternationalCurrency = AParameters["param_currency"].ToString().ToLower().StartsWith("int");
                Double ExchangeRate = 1.00;  // TODO Get exchange rate!

                foreach (DataRow r in resultTable.Rows)
                {
                    if (InternationalCurrency)
                    {
                        r["GiftIntlAmount"] = (Decimal)(Convert.ToDouble(r["GiftBaseAmount"]) * ExchangeRate);
                    }

                    r["Reference"] = StringHelper.PartnerKeyToStr(Convert.ToInt64(r["RecipientKey"]));
                }

                return resultTable;
            }   // try
            catch (Exception e)
            {
                TLogging.Log("Problem gift rows for HOSA report: " + e.ToString());
                return null;
            }
            finally
            {
                if (NewTransaction)
                {
                    DBAccess.GDBAccessObj.RollbackTransaction();
                }
            }
        }
    }
}