using System;
using Microsoft.AnalysisServices.Tabular;
using ProgrammingDatasets.Models;

namespace ProgrammingDatasets {
  class Program {

    static void Main() {

      Demo01();
      //Demo02();
      //Demo03();
      //Demo04();
      //Demo05();
      //Demo06();
      //Demo07();
      //Demo08();
      //Demo09();
      //Demo10();
      //Demo11();
    }

    #region "Call Power BI REST API as User"

    static void Demo01() {
      DatasetManager.DisplayWorkspaces();
      DatasetManager.DisplayWorkspacesAsAdmin();
    }

    static void Demo02() {
      var workspace = DatasetManager.GetWorkspace("Acme Corp");
      var dataset = DatasetManager.GetDataset(workspace.Id, "Sales");
      DatasetManager.DisplayDatasetInfo(workspace.Id, dataset.Id);
    }

    static void Demo03() {
      string workspaceName = "Tenant 01";
      string importName = "Sales";
      var workspace = DatasetManager.CreatWorkspace(workspaceName);
      var import = DatasetManager.ImportPBIX(workspace.Id, Properties.Resources.SalesReportTemplate_pbix, importName);
    }

    static void Demo04() {
      DatasetManager.DeleteAllWorkspaces();
      DatasetManager.OnboardNewTenant(SampleTenants.Wingtip);
      DatasetManager.OnboardNewTenant(SampleTenants.Contoso);
      DatasetManager.OnboardNewTenant(SampleTenants.AcmeCorp);
      DatasetManager.OnboardNewTenant(SampleTenants.MegaCorp);
    }

    static void Demo05() {
      string workspaceName = "Tenant 01";
      //var workspace = DatasetManager.CreatWorkspace(workspaceName);
       var workspace = DatasetManager.GetWorkspace(workspaceName);
      Guid capacityId = new Guid(AppSettings.dedicatedCapacityId);
      DatasetManager.AssignWorkspaceToCapacity(workspace, capacityId);
      //DatasetManager.AssignWorkspaceToSharedCapacity(workspace);
    }

    #endregion

    #region "Call Power BI REST API as Service Principal"

    static void Demo06() {
      // DatasetManager.AddServicePrincipalAsAdmin();
      DatasetManagerServicePrincipal.DisplayWorkspaces();
    }

    static void Demo07() {
      DatasetManagerServicePrincipal.DeleteAllWorkspaces();
      DatasetManagerServicePrincipal.OnboardNewTenant(SampleTenants.Wingtip);
      DatasetManagerServicePrincipal.OnboardNewTenant(SampleTenants.Contoso);
      DatasetManagerServicePrincipal.OnboardNewTenant(SampleTenants.AcmeCorp);
      DatasetManagerServicePrincipal.OnboardNewTenant(SampleTenants.MegaCorp);
    }

    #endregion

    #region "Call Tabular Object Model (TOM) as User"

    static void Demo08() {
      DatasetManagerTom.DisplayDatabases();
      // DatasetManagerTom.DisplayDatabaseInfo("Wingtip Sales");
      // DatasetManagerTom.DisplayTableInfo("Wingtip Sales", "Table");
    }

    static void Demo09() {

      // upload PBIX file which uses local Excel file
      string workspaceName = "Test";
      string pbixFilePath = @"C:\DevCamp\ProgrammingDatasets\LocalFiles\SalesDemo.pbix";
      string importName = "Sales";
      var workspace = DatasetManager.GetWorkspace(workspaceName);
      var import = DatasetManager.ImportPBIX(workspace.Id, pbixFilePath, importName);

      // overwrite M code behind query to redirect datasource to ADLS
      DatasetManagerTom.UpdateTableQuery("Sales", "Sales");

    }

    static void Demo10() {
      string newDatabaseName = "Wingtip Sales";
      Database database = DatasetManagerTom.CreateDatabase(newDatabaseName);
      DatasetManagerTom.CreateWingtipSalesModel(database);
    }

    static void Demo11() {
      // execute DAX query and convert output to CSV format
      string WorkspaceName = "Wingtip Sales";
      DaxQueryManager.ConvertDaxQueryToCsv(WorkspaceName, Properties.Resources.QueryGetSalesByState_dax, "SalesByState.csv");
    }

    #endregion

  }

}
