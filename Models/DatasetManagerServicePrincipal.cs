using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Extensions;
using Microsoft.PowerBI.Api.Models;
using Microsoft.PowerBI.Api.Models.Credentials;
using Microsoft.Rest;

namespace ProgrammingDatasets.Models {

  class DatasetManagerServicePrincipal {

    public static void DisplayWorkspaces() {
      Console.WriteLine("Running DisplayWorkspaces");
      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();

      var workspaces = pbiClient.Groups.GetGroups().Value;
      if (workspaces.Count == 0) {
        Console.WriteLine("There are no workspaces for this service principal");
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
      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();
      string filter = "state ne 'Deleted'";
      var workspaces = pbiClient.Groups.GetGroupsAsAdmin(top: 100, filter: filter).Value;
      foreach (var workspace in workspaces) {
        Console.WriteLine("  " + workspace.Name + " - [" + workspace.Id + "]");
      }
      Console.WriteLine();
    }

    public static Group GetWorkspace(string Name) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();
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
      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();
      
      GroupCreationRequest request = new GroupCreationRequest(Name);
      Group workspace = pbiClient.Groups.CreateGroup(request);

      // add yourself as Admin to workspaces create by service principal
      string adminUser = AppSettings.adminUser;
      if (!string.IsNullOrEmpty(adminUser)) {
        pbiClient.Groups.AddGroupUser(workspace.Id, new GroupUser {
          EmailAddress = adminUser,
          GroupUserAccessRight = "Admin"
        });
      }

      return workspace;
    }

    public void DeleteWorkspace(Guid WorkspaceId) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();
      pbiClient.Groups.DeleteGroup(WorkspaceId);
    }

    public static Dataset GetDataset(Guid WorkspaceId, string DatasetName) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();
      var datasets = pbiClient.Datasets.GetDatasetsInGroup(WorkspaceId).Value;
      foreach (var dataset in datasets) {
        if (dataset.Name.Equals(DatasetName)) {
          return dataset;
        }
      }
      return null;
    }

    public static void DisplayDatasetInfo(Guid WorkspaceId, string DatasetId) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();
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
      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();

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
      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();
      MemoryStream stream = new MemoryStream(PbixContent);
      var import = pbiClient.Imports.PostImportWithFileInGroup(WorkspaceId, stream, ImportName, ImportConflictHandlerMode.CreateOrOverwrite);

      do { import = pbiClient.Imports.GetImportInGroup(WorkspaceId, import.Id); }
      while (import.ImportState.Equals("Publishing"));

      return import;
    }

    public static void PatchSqlDatasourceCredentials(Guid WorkspaceId, string DatasetId, string SqlUserName, string SqlUserPassword) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();

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
      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();
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

    public static void PatchAdlsCredentials(Guid WorkspaceId, string DatasetId) {

      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();
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

    public static void UpdateSqlDatabaseConnectionString(Guid WorkspaceId, string DatasetId, string Server, string Database) {

      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();
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

    public static void RefreshDataset(Guid WorkspaceId, string DatasetId) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();
      pbiClient.Datasets.RefreshDatasetInGroup(WorkspaceId, DatasetId);
    }

    public static void UpdateParameter(Guid WorkspaceId, string DatasetId, string ParameterName, string ParameterValue) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();
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
      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();

      Console.WriteLine("Starting tenant onboarding process for " + Tenant.Name);
      Console.WriteLine(" - Creating workspace for " + Tenant.Name + "...");
      GroupCreationRequest request = new GroupCreationRequest(Tenant.Name);
      Group workspace = null;

      try {
        workspace = pbiClient.Groups.CreateGroup(request);
      }
      catch (HttpOperationException ex) {
        // handle exception if workspace name already exists
        if (ex.Response.Content.Contains("PowerBIEntityAlreadyExists")) {
          // make sure name is unqiue and reattempt workspace creation
          Tenant.Name = Tenant.Name + " (" + (Guid.NewGuid().ToString().Substring(0, 4)) + ")";
          Console.WriteLine(" - Workspace name already used - changing name to " + Tenant.Name);
          workspace = pbiClient.Groups.CreateGroup(new GroupCreationRequest(Tenant.Name));

        }
        else {
          Console.WriteLine("Could not create workspace for " + Tenant.Name);
          return;
        }
      }

      // assign workspace to dedicated capacity in production scenarios
      // Guid capacityId = new Guid("99999999-9999-9999-9999-999999999");
      // pbiClient.Groups.AssignToCapacity(workspace.Id, new AssignToCapacityRequest(capacityId));

      Console.WriteLine(" - Importing PBIX teplate file...");
      string importName = "Sales";
      var import = DatasetManagerServicePrincipal.ImportPBIX(workspace.Id, Properties.Resources.SalesReportTemplate_pbix, importName);

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

    public static void DeleteAllWorkspaces() {
      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();
      var workspaces = pbiClient.Groups.GetGroups().Value;
      foreach (var workspace in workspaces) {
        Console.WriteLine("Deleting " + workspace.Name);
        pbiClient.Groups.DeleteGroup(workspace.Id);
      }
      Console.WriteLine();
    }

    public static void GetGateways() {
      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();

      var gateways = pbiClient.Gateways.GetGateways().Value;

      foreach (var gateway in gateways) {
        Console.WriteLine(gateway.Id);
        Console.WriteLine(gateway.Name);
        Console.WriteLine(gateway.PublicKey);
        Console.WriteLine(gateway.Type);
        Console.WriteLine(gateway.GatewayStatus);
        Console.WriteLine();
        Console.WriteLine("Gateway datasources for " + gateway.Name);

        var datasources = pbiClient.Gateways.GetDatasources(gateway.Id).Value;
        foreach (var datasource in datasources) {
          Console.WriteLine(" - Name: " + datasource.DatasourceName);
          Console.WriteLine(" - Id: " + datasource.Id);
          Console.WriteLine(" - DatasourceType: " + datasource.DatasourceType);
          Console.WriteLine(" - DatasourceName: " + datasource.DatasourceName);
          Console.WriteLine(" - ConnectionDetails: " + datasource.ConnectionDetails);
          Console.WriteLine(" - CredentialType: " + datasource.CredentialType);
          Console.WriteLine();
        }
        Console.WriteLine();

      }

    }

    public static void AddGatewayDatasourceLocalSql() {

      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();
      Gateway gateway = pbiClient.Gateways.GetGateways().Value[0];
      Guid gatewayId = gateway.Id;

      var credentialsEncryptor = new AsymmetricKeyEncryptor(gateway.PublicKey);
      var credentials = new WindowsCredentials(username: @"DIONYSUS\TedP", password: "Wacko@43");

      PublishDatasourceToGatewayRequest requestToAddDatasource = new PublishDatasourceToGatewayRequest {
        DataSourceName = "Wingtip Sales on Dionysus",
        DataSourceType = "SQL",
        ConnectionDetails = "{\"server\":\"DIONYSUS\",\"database\":\"WingtipSales\"}",
        CredentialDetails = new CredentialDetails(credentials, PrivacyLevel.None, EncryptedConnection.Encrypted, credentialsEncryptor)
      };

      pbiClient.Gateways.CreateDatasource(gatewayId, requestToAddDatasource);
    }

    public static void AddGatewayDatasourceAzureSql() {

      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();

      Gateway gateway = pbiClient.Gateways.GetGateways().Value[0];
      Guid gatewayId = gateway.Id;

      var credentials = new BasicCredentials(username: @"CptStudent", password: "pass@word1");
      var credentialsEncryptor = new AsymmetricKeyEncryptor(gateway.PublicKey);

      PublishDatasourceToGatewayRequest requestToAddDatasource = new PublishDatasourceToGatewayRequest {
        DataSourceName = "Wingtip Sales on DevCamp.Database.Windows.net",
        DataSourceType = "SQL",
        ConnectionDetails = "{\"server\":\"DevCamp.Database.Windows.net\",\"database\":\"WingtipSales\"}",
        CredentialDetails = new CredentialDetails(credentials, PrivacyLevel.Organizational, EncryptedConnection.Encrypted, credentialsEncryptor)
      };

      pbiClient.Gateways.CreateDatasource(gatewayId, requestToAddDatasource);
    }

    public static void AddGatewayDatasourceForAzureBlob() {

      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();

      Gateway gateway = pbiClient.Gateways.GetGateways().Value[0];
      Guid gatewayId = gateway.Id;

      var credentials = new KeyCredentials(AppSettings.adlsStorageKey);
      var credentialsEncryptor = new AsymmetricKeyEncryptor(gateway.PublicKey);

      string accountName = "powerbidevcamp2";
      string accountDomain = "blob.core.windows.net";

      PublishDatasourceToGatewayRequest requestToAddDatasource = new PublishDatasourceToGatewayRequest {
        DataSourceName = "Azure Blob Sorage",
        DataSourceType = "AzureBlobs",

        ConnectionDetails = "{\"account\":\"" + accountName + "\",\"domain\":\"" + accountDomain + "\"}",
        CredentialDetails = new CredentialDetails(credentials, PrivacyLevel.Organizational, EncryptedConnection.Encrypted, credentialsEncryptor)
      };

      pbiClient.Gateways.CreateDatasource(gatewayId, requestToAddDatasource);
    }

    public static void AddGatewayDatasourceForSharePointSite() {

      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();

      Gateway gateway = pbiClient.Gateways.GetGateways().Value[0];
      Guid gatewayId = gateway.Id;

      var credentials = new OAuth2Credentials("");
      var credentialsEncryptor = new AsymmetricKeyEncryptor(gateway.PublicKey);

      string accountName = "powerbidevcamp2";
      string accountDomain = "blob.core.windows.net";

      PublishDatasourceToGatewayRequest requestToAddDatasource = new PublishDatasourceToGatewayRequest {
        DataSourceName = "Azure Blob Sorage",
        DataSourceType = "AzureBlobs",

        ConnectionDetails = "{\"account\":\"" + accountName + "\",\"domain\":\"" + accountDomain + "\"}",
        CredentialDetails = new CredentialDetails(credentials, PrivacyLevel.Organizational, EncryptedConnection.Encrypted, credentialsEncryptor)
      };

      pbiClient.Gateways.CreateDatasource(gatewayId, requestToAddDatasource);
    }

    public static void AddGatewayDatasource() {
      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();


      Guid gatewayId = new Guid("f7c32bd9-40af-42a8-8a2c-a1f92438fb03");

      var gateway = pbiClient.Gateways.GetGateway(gatewayId);

      var credentialsEncryptor = new AsymmetricKeyEncryptor(gateway.PublicKey);

      var credentials = new WindowsCredentials(username: @"DIONYSUS\TedP", password: "Wacko@43");

      PublishDatasourceToGatewayRequest requestAddDatasource = new PublishDatasourceToGatewayRequest {
        DataSourceName = "Wingtip Sales on Dionysus",
        DataSourceType = "SQL",
        ConnectionDetails = "{\"server\":\"DIONYSUS\",\"database\":\"WingtipSales\"}",
        CredentialDetails = new CredentialDetails(credentials, PrivacyLevel.None, EncryptedConnection.Encrypted, credentialsEncryptor)
      };

      pbiClient.Gateways.CreateDatasource(gatewayId, requestAddDatasource);
    }

    public static void AddGatewayDatasourceUser() {
      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();

      var gateways = pbiClient.Gateways.GetGateways().Value;

      foreach (var gateway in gateways) {
        Console.WriteLine(gateway.Id);
        Console.WriteLine(gateway.Name);
        Console.WriteLine(gateway.PublicKey);
        Console.WriteLine(gateway.Type);
        Console.WriteLine(gateway.GatewayStatus);
        Console.WriteLine();
        Console.WriteLine("Gateway datasources:");
        var datasources = pbiClient.Gateways.GetDatasources(gateway.Id).Value;
        foreach (var datasource in datasources) {
          Console.WriteLine(" - " + datasource.DatasourceName);
          var datasourceUser = new DatasourceUser();
          datasourceUser.DatasourceAccessRight = DatasourceUserAccessRight.Read;
          datasourceUser.EmailAddress = "AustinP@powerbidevcamp.net";
          pbiClient.Gateways.AddDatasourceUser(gateway.Id, datasource.Id, datasourceUser);
        }
        Console.WriteLine();
      }

    }

    public static void AddGatewayDatasourceServicePrincipal() {
      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();

      var gateways = pbiClient.Gateways.GetGateways().Value;

      foreach (var gateway in gateways) {
        Console.WriteLine(gateway.Id);
        Console.WriteLine(gateway.Name);
        Console.WriteLine(gateway.PublicKey);
        Console.WriteLine(gateway.Type);
        Console.WriteLine(gateway.GatewayStatus);
        Console.WriteLine();
        Console.WriteLine("Gateway datasources:");
        var datasources = pbiClient.Gateways.GetDatasources(gateway.Id).Value;
        foreach (var datasource in datasources) {
          Console.WriteLine(" - " + datasource.DatasourceName);
          var datasourceUser = new DatasourceUser();
          datasourceUser.DatasourceAccessRight = DatasourceUserAccessRight.Read;
          datasourceUser.Identifier = "7faa8784-9fe7-4020-a0fd-857e9a897105";
          datasourceUser.PrincipalType = PrincipalType.App;
          pbiClient.Gateways.AddDatasourceUser(gateway.Id, datasource.Id, datasourceUser);
        }
        Console.WriteLine();
      }

    }

    public static void DiscoverGatewaysInGroup(Guid WorkspaceId, string DatasetId) {

      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();

      //Dataset dataset = pbiClient.Datasets.GetDatasetInGroup(WorkspaceId, DatasetId);
      Gateways gateways = pbiClient.Datasets.DiscoverGatewaysInGroup(WorkspaceId, DatasetId);


      Gateway gateway = pbiClient.Gateways.GetGateways().Value[0];

      BindToGatewayRequest bindRequest = new BindToGatewayRequest();
      bindRequest.GatewayObjectId = gateway.Id;
      bindRequest.DatasourceObjectIds = new List<Guid?>()
      {
        new Guid("61bac0a7-c422-4b48-9725-762f10728dc7")
      };

      pbiClient.Datasets.BindToGatewayInGroup(WorkspaceId, DatasetId, bindRequest);
    }

    public static void BindDatasetToGatewayDatasource(Guid WorkspaceId, string DatasetId) {

      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();

      Gateway gateway = pbiClient.Gateways.GetGateways().Value[0];

      BindToGatewayRequest bindRequest = new BindToGatewayRequest();

      bindRequest.GatewayObjectId = gateway.Id;

      pbiClient.Datasets.TakeOverInGroup(WorkspaceId, DatasetId);

      pbiClient.Datasets.BindToGatewayInGroup(WorkspaceId, DatasetId, bindRequest);
    }

    public static void BindDatasetToGatewayDatasource2(string WorkspaceId, string DatasetId) {

      PowerBIClient pbiClient = TokenManager.GetPowerBiAppOnlyClient();
      IList<Dataset> datasets = pbiClient.Datasets.GetDatasetsInGroup(new Guid(WorkspaceId)).Value;

      var dataset = datasets.Where(ds => ds.Id.Equals(DatasetId)).Single();

      Console.WriteLine(dataset.Name);

      IList<Datasource> datasources = pbiClient.Datasets.GetDatasourcesInGroup(new Guid(WorkspaceId), DatasetId).Value;

      foreach (var ds in datasources) {
        Console.WriteLine(ds.Name);
      }

      IList<Refresh> refreshes = null;
      if (dataset.IsRefreshable == true) {
        refreshes = pbiClient.Datasets.GetRefreshHistoryInGroup(new Guid(WorkspaceId), DatasetId).Value;
        foreach (var refresh in refreshes) {
          Console.WriteLine(refresh.RefreshType.Value + ": " + refresh.StartTime.Value.ToLocalTime());
        }
      }


    }

  }
}
