using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Extensions;
using Microsoft.PowerBI.Api.Models;
using Microsoft.PowerBI.Api.Models.Credentials;

using AMO = Microsoft.AnalysisServices;
using TOM = Microsoft.AnalysisServices.Tabular;

namespace ProgrammingDatasets.Models {

  class DatasetManager {

    public static void DisplayWorkspaces() {
      Console.WriteLine("Running DisplayWorkspaces");
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.ReadWorkspaces);
      var workspaces = pbiClient.Groups.GetGroups().Value;
      if (workspaces.Count == 0) {
        Console.WriteLine("There are no workspaces for this user");
      }
      else {
        foreach (var workspace in workspaces) {
          Console.WriteLine("  " + workspace.Name + " - [" + workspace.Id + "]");
        }
      }
      Console.WriteLine();
    }

    public static void DisplayWorkspacesAsAdmin() {
      Console.WriteLine("Running DisplayWorkspacesAsAdmin");
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.TenantReadAll);
      string filter = "state ne 'Deleted'";
      var workspaces = pbiClient.Groups.GetGroupsAsAdmin(top: 100, filter: filter).Value;
      foreach (var workspace in workspaces) {
        Console.WriteLine("  " + workspace.Name + " - [" + workspace.Id + "]");
      }
      Console.WriteLine();
    }

    public static Group GetWorkspace(string Name) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.ReadWorkspaces);
      // build search filter
      string filter = "name eq '" + Name + "'";
      var workspaces = pbiClient.Groups.GetGroups(filter: filter).Value;
      if (workspaces.Count == 0) {
        return null;
      }
      else {
        return workspaces.First();
      }
    }

    public static Group CreatWorkspace(string Name) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.TenantProvisioning);

      GroupCreationRequest request = new GroupCreationRequest(Name);
      Group workspace = pbiClient.Groups.CreateGroup(request);
      return workspace;
    }

    public void DeleteWorkspace(Guid WorkspaceId) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.TenantProvisioning);
      pbiClient.Groups.DeleteGroup(WorkspaceId);
    }

    public static Dataset GetDataset(Guid WorkspaceId, string DatasetName) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.ReadWorkspaceAssets);
      var datasets = pbiClient.Datasets.GetDatasetsInGroup(WorkspaceId).Value;
      foreach (var dataset in datasets) {
        if (dataset.Name.Equals(DatasetName)) {
          return dataset;
        }
      }
      return null;
    }

    public static void DisplayDatasetInfo(Guid WorkspaceId, string DatasetId) {

      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.ReadWorkspaceAssets);
      IList<Dataset> datasets = pbiClient.Datasets.GetDatasetsInGroup(WorkspaceId).Value;

      var dataset = datasets.Where(ds => ds.Id.Equals(DatasetId)).Single();

      var newline = Environment.NewLine;

      Console.WriteLine("Name: " + dataset.Name);
      Console.WriteLine("ConfiguredBy: " + dataset.ConfiguredBy);
      Console.WriteLine();
      Console.WriteLine("IsEffectiveIdentityRequired: " + dataset.IsEffectiveIdentityRequired);
      Console.WriteLine("IsEffectiveIdentityRolesRequired: " + dataset.IsEffectiveIdentityRolesRequired);
      Console.WriteLine("IsOnPremGatewayRequired: " + dataset.IsOnPremGatewayRequired);
      Console.WriteLine();

      Console.WriteLine("Datasources:");
      IList<Datasource> datasources = pbiClient.Datasets.GetDatasourcesInGroup(WorkspaceId, DatasetId).Value;
      foreach (var datasource in datasources) {
        Console.WriteLine("  [" + datasource.DatasourceType + "] server=" +
                          datasource.ConnectionDetails.Server + " database=" +
                          datasource.ConnectionDetails.Database);
      }
      Console.WriteLine();

      IList<Refresh> refreshes = null;
      if (dataset.IsRefreshable == true) {
        Console.WriteLine("Refresh History:");
        refreshes = pbiClient.Datasets.GetRefreshHistoryInGroup(WorkspaceId, DatasetId).Value;
        if (refreshes.Count == 0) {
          Console.WriteLine("  This dataset has never been refreshed.");
        }
        else {
          foreach (var refresh in refreshes) {
            Console.WriteLine("  " + refresh.RefreshType.Value +
                              " refresh on " + refresh.StartTime.Value.ToShortDateString() +
                              " | Started: " + refresh.StartTime.Value.ToLocalTime().ToLongTimeString() +
                              " | Completed:  " + refresh.EndTime.Value.ToLocalTime().ToLongTimeString());
          }
        }
      }

      Console.WriteLine();
    }

    public static Import ImportPBIX(Guid WorkspaceId, string PbixFilePath, string ImportName) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.TenantProvisioning);

      // open PBIX in file stream
      FileStream stream = new FileStream(PbixFilePath, FileMode.Open, FileAccess.Read);

      // post import to start import process
      var import = pbiClient.Imports.PostImportWithFileInGroup(WorkspaceId, stream, ImportName, ImportConflictHandlerMode.CreateOrOverwrite);

      // poll to determine when import operation has complete
      do { import = pbiClient.Imports.GetImportInGroup(WorkspaceId, import.Id); }
      while (import.ImportState.Equals("Publishing"));

      // return Import object to caller
      return import;
    }

    public static Import ImportPBIX(Guid WorkspaceId, byte[] PbixContent, string ImportName) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.TenantProvisioning);

      MemoryStream stream = new MemoryStream(PbixContent);
      var import = pbiClient.Imports.PostImportWithFileInGroup(WorkspaceId, stream, ImportName, ImportConflictHandlerMode.CreateOrOverwrite);

      do { import = pbiClient.Imports.GetImportInGroup(WorkspaceId, import.Id); }
      while (import.ImportState.Equals("Publishing"));

      return import;
    }

    public static void PatchSqlDatasourceCredentials(Guid WorkspaceId, string DatasetId, string SqlUserName, string SqlUserPassword) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.TenantProvisioning);

      var datasources = (pbiClient.Datasets.GetDatasourcesInGroup(WorkspaceId, DatasetId)).Value;

      // find the target SQL datasource
      foreach (var datasource in datasources) {
        if (datasource.DatasourceType.ToLower() == "sql") {
          // get the datasourceId and the gatewayId
          var datasourceId = datasource.DatasourceId;
          var gatewayId = datasource.GatewayId;
          // Create UpdateDatasourceRequest to update Azure SQL datasource credentials
          UpdateDatasourceRequest req = new UpdateDatasourceRequest {
            CredentialDetails = new CredentialDetails(
              new BasicCredentials(SqlUserName, SqlUserPassword),
              PrivacyLevel.None,
              EncryptedConnection.NotEncrypted)
          };
          // Execute Patch command to update Azure SQL datasource credentials
          pbiClient.Gateways.UpdateDatasource((Guid)gatewayId, (Guid)datasourceId, req);
        }
      };

    }

    public static void PatchAnonymousDatasourceCredentials(Guid WorkspaceId, string DatasetId) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.TenantProvisioning);

      var datasources = pbiClient.Datasets.GetDatasourcesInGroup(WorkspaceId, DatasetId).Value;
      foreach (var datasource in datasources) {
        if (datasource.DatasourceType == "OAuth" || datasource.DatasourceType == "File") {
          var datasourceId = datasource.DatasourceId;
          var gatewayId = datasource.GatewayId;
          // create credentials for Azure SQL database log in
          CredentialDetails details = new CredentialDetails {
            CredentialType = CredentialType.Anonymous,
            PrivacyLevel = PrivacyLevel.None
          };
          UpdateDatasourceRequest req = new UpdateDatasourceRequest(details);
          // Update credentials through gateway
          pbiClient.Gateways.UpdateDatasourceAsync((Guid)gatewayId, (Guid)datasourceId, req);
        }
      }
      return;
    }

    public static void UpdateSqlDatabaseConnectionString(Guid WorkspaceId, string DatasetId, string Server, string Database) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.TenantProvisioning);

      Datasource targetDatasource = pbiClient.Datasets.GetDatasourcesInGroup(WorkspaceId, DatasetId).Value.First();

      string currentServer = targetDatasource.ConnectionDetails.Server;
      string currentDatabase = targetDatasource.ConnectionDetails.Database;

      if (Server.ToLower().Equals(currentServer.ToLower()) && Database.ToLower().Equals(currentDatabase.ToLower())) {
        Console.WriteLine("New server and database name are the same as the old names");
        return;
      }

      DatasourceConnectionDetails connectionDetails = new DatasourceConnectionDetails {
        Database = Database,
        Server = Server
      };

      UpdateDatasourceConnectionRequest updateConnRequest =
        new UpdateDatasourceConnectionRequest {
          DatasourceSelector = targetDatasource,
          ConnectionDetails = connectionDetails
        };

      UpdateDatasourcesRequest updateDatasourcesRequest = new UpdateDatasourcesRequest(updateConnRequest);
      pbiClient.Datasets.UpdateDatasourcesInGroup(WorkspaceId, DatasetId, updateDatasourcesRequest);

    }

    public static void PatchAdlsCredentials(Guid WorkspaceId, string DatasetId) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.TenantProvisioning);

      pbiClient.Datasets.TakeOverInGroup(WorkspaceId, DatasetId);
      var datasources = (pbiClient.Datasets.GetDatasourcesInGroup(WorkspaceId, DatasetId)).Value;
      // find the target SQL datasource
      foreach (var datasource in datasources) {
        if (datasource.DatasourceType.ToLower() == "azureblobs") {
          // get the datasourceId and the gatewayId
          var datasourceId = datasource.DatasourceId;
          var gatewayId = datasource.GatewayId;
          // Create UpdateDatasourceRequest to update Azure SQL datasource credentials
          UpdateDatasourceRequest req = new UpdateDatasourceRequest {
            CredentialDetails = new CredentialDetails(
              new KeyCredentials(AppSettings.adlsStorageKey),
              PrivacyLevel.None,
              EncryptedConnection.NotEncrypted)
          };
          // Execute Patch command to update Azure SQL datasource credentials
          pbiClient.Gateways.UpdateDatasource((Guid)gatewayId, (Guid)datasourceId, req);
        }
      };

    }

    public static void RefreshDataset(Guid WorkspaceId, string DatasetId) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.TenantProvisioning);
      pbiClient.Datasets.RefreshDatasetInGroup(WorkspaceId, DatasetId);
    }

    public static void UpdateParameter(Guid WorkspaceId, string DatasetId, string ParameterName, string ParameterValue) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.TenantProvisioning);

      IList<Dataset> datasets = pbiClient.Datasets.GetDatasetsInGroup(WorkspaceId).Value;
      var dataset = datasets.Where(ds => ds.Id.Equals(DatasetId)).Single();
      UpdateMashupParametersRequest req =
        new UpdateMashupParametersRequest(
          new UpdateMashupParameterDetails {
            Name = ParameterName,
            NewValue = ParameterValue
          });
      pbiClient.Datasets.UpdateParametersInGroup(WorkspaceId, DatasetId, req);
    }

    public static void OnboardNewTenant(PowerBiTenant Tenant) {
      Console.WriteLine("Starting tenant onboarding process for " + Tenant.Name);
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.TenantProvisioning);

      Console.WriteLine(" - Creating workspace for " + Tenant.Name + "...");
      GroupCreationRequest request = new GroupCreationRequest(Tenant.Name);
      Group workspace = pbiClient.Groups.CreateGroup(request);

      // uncomment to assign workspace to dedicated capacity when in production scenarios
      // Guid capacityId = new Guid("99999999-9999-9999-9999-999999999");
      // pbiClient.Groups.AssignToCapacity(workspace.Id, new AssignToCapacityRequest(capacityId));

      Console.WriteLine(" - Importing PBIX teplate file...");
      string importName = "Sales";
      var import = DatasetManager.ImportPBIX(workspace.Id, Properties.Resources.SalesReportTemplate_pbix, importName);

      Dataset dataset = GetDataset(workspace.Id, importName);

      Console.WriteLine(" - Updating dataset parameters...");
      UpdateMashupParametersRequest req =
        new UpdateMashupParametersRequest(new List<UpdateMashupParameterDetails>() {
          new UpdateMashupParameterDetails { Name = "DatabaseServer", NewValue = Tenant.DatabaseServer },
          new UpdateMashupParameterDetails { Name = "DatabaseName", NewValue = Tenant.DatabaseName }
      });

      pbiClient.Datasets.UpdateParametersInGroup(workspace.Id, dataset.Id, req);

      Console.WriteLine(" - Patching datasourcre credentials...");
      PatchSqlDatasourceCredentials(workspace.Id, dataset.Id, Tenant.DatabaseUserName, Tenant.DatabaseUserPassword);

      Console.WriteLine(" - Starting dataset refresh operation...");
      pbiClient.Datasets.RefreshDatasetInGroup(workspace.Id, dataset.Id);

      Console.WriteLine(" - Tenant onboarding processing completed");
      Console.WriteLine();
    }

    public static void OnboardNewTenantWithSecondaryReport(PowerBiTenant Tenant) {
      Console.WriteLine("Starting tenant onboarding process for " + Tenant.Name);
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.TenantProvisioning);

      Console.WriteLine(" - Creating workspace for " + Tenant.Name + "...");
      GroupCreationRequest request = new GroupCreationRequest(Tenant.Name);
      Group workspace = pbiClient.Groups.CreateGroup(request);

      // assign workspace to dedicated capacity in production scenarios
      // Guid capacityId = new Guid("99999999-9999-9999-9999-999999999");
      // pbiClient.Groups.AssignToCapacity(workspace.Id, new AssignToCapacityRequest(capacityId));

      Console.WriteLine(" - Importing PBIX teplate file...");
      string importName = "Sales";
      var import = DatasetManager.ImportPBIX(workspace.Id, Properties.Resources.SalesReportTemplate_pbix, importName);

      Dataset dataset = GetDataset(workspace.Id, importName);

      Console.WriteLine(" - Updating dataset parameters...");
      UpdateMashupParametersRequest req =
        new UpdateMashupParametersRequest(new List<UpdateMashupParameterDetails>() {
          new UpdateMashupParameterDetails { Name = "DatabaseServer", NewValue = Tenant.DatabaseServer },
          new UpdateMashupParameterDetails { Name = "DatabaseName", NewValue = Tenant.DatabaseName }
      });

      pbiClient.Datasets.UpdateParametersInGroup(workspace.Id, dataset.Id, req);

      Console.WriteLine(" - Patching datasourcre credentials...");
      PatchSqlDatasourceCredentials(workspace.Id, dataset.Id, Tenant.DatabaseUserName, Tenant.DatabaseUserPassword);

      Console.WriteLine(" - Starting dataset refresh operation...");
      pbiClient.Datasets.RefreshDatasetInGroup(workspace.Id, dataset.Id);

      Console.WriteLine(" - Uploading and binding secondary report...");
      var secondaryReportImport = DatasetManager.ImportPBIX(workspace.Id, Properties.Resources.SecondarySalesReport_pbix, "Sales by State");
      var secondaryReportId = secondaryReportImport.Reports.First().Id;
      var rebindRequest = new RebindReportRequest(dataset.Id);
      pbiClient.Reports.RebindReportInGroup(workspace.Id, secondaryReportId, rebindRequest);

      Console.WriteLine(" - Tenant onboarding processing completed");
      Console.WriteLine();
    }

    public static void DeleteAllWorkspaces() {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.TenantProvisioning);
      var workspaces = pbiClient.Groups.GetGroups().Value;
      foreach (var workspace in workspaces) {
        Console.WriteLine("Deleting " + workspace.Name);
        pbiClient.Groups.DeleteGroup(workspace.Id);
      }
      Console.WriteLine();
    }

    public static void AddServicePrincipalAsAdmin() {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.TenantReadWriteAll);

      // get Service Principal Object Id (and not ApplicationID) to assign membership
      string servicePrincipalObjectId = AppSettings.servicePrincipalObjectId; ;

      var workspaces = pbiClient.Groups.GetGroups().Value;
      // enumerate though each workspace
      foreach (var workspace in workspaces) {
        var members = pbiClient.Groups.GetGroupUsers(workspace.Id).Value;
        var memberSearchResults = members.Where(user => user.Identifier.Equals(servicePrincipalObjectId));
        if (memberSearchResults.Count() == 0) {
          pbiClient.Groups.AddGroupUser(workspace.Id, new GroupUser {
            PrincipalType = "App",
            Identifier = servicePrincipalObjectId,
            GroupUserAccessRight = "Admin"
          });
        }
        else if (memberSearchResults.First().GroupUserAccessRight != "Admin") {
          pbiClient.Groups.UpdateGroupUser(workspace.Id, new GroupUser {
            PrincipalType = "App",
            Identifier = servicePrincipalObjectId,
            GroupUserAccessRight = "Admin"
          });
        }
      }
    }

    public static void GetCapacities() {
      Console.WriteLine("GetCapacities");
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.TenantProvisioning);
      var capacities = pbiClient.Capacities.GetCapacities().Value;
      foreach (var capacity in capacities) {
        Console.WriteLine(capacity.Id);
      }
    }

    public static void AssignWorkspaceToCapacity(Group workspace, Guid capacityId) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.TenantProvisioning);
      pbiClient.Groups.AssignToCapacity(workspace.Id, new AssignToCapacityRequest(capacityId));
    }

    public static void AssignWorkspaceToSharedCapacity(Group workspace) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient(PowerBiPermissionScopes.TenantProvisioning);
      Guid sharedCapacityId = new Guid("00000000-0000-0000-0000-000000000000");
      pbiClient.Groups.AssignToCapacity(workspace.Id, new AssignToCapacityRequest(sharedCapacityId));
    }

  }
}
