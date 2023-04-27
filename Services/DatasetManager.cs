using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.PowerBI.Api.Models.Credentials;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace SettingDatasourceCredentials.Services {

  public class DatasetManager {

    private static PowerBIClient pbiClient;
    private static bool CallAsUser = false;

    static DatasetManager() {
      if (CallAsUser) {
        pbiClient = TokenManager.GetPowerBiClientForUser();
      }
      else {
        pbiClient = TokenManager.GetPowerBiClientForServicePrincipal();
      }
    }

    #region "Methods for Provisioning Workspaces and Datasets"

    public static void OnboardNewCustomerTenant(string TenantName) {

      Group workspace = CreatWorkspace(TenantName);
      AssignWorkspaceToCapacity(workspace);

      if (CallAsUser == false) {
        AddDemoAdminUser(workspace);
      }

      // Demo 1 - patch Web datasource with Anonymous credentials
      ProvisionDatasetWithAnonymousDatasource(workspace, "Anonymous Datasource - Product Sales");

      // Demo 2 - patch SQL datasource with Basic credentials
     ProvisionDatasetWithSqlDatasource(workspace, "SQL Datasource - Wingtip Sales", AppSettings.SqlDatabaseServer, AppSettings.SqlDatabaseWingtip);
     ProvisionDatasetWithSqlDatasource(workspace, "SQL Datasource - Contoso Sales", AppSettings.SqlDatabaseServer, AppSettings.SqlDatabaseContoso);

      // Demo 3 - patch SQL datasource for paginated report with Basic credentials
      ProvisionPaginatedReportWithSqlDatasource(workspace.Id, "SQL Datasource for Paginated Report");

      // Demo 4 - patch SQL datasource for dataflow using Basic credentials
      ProvisionDatasetWithDataflowDatasource(workspace.Id, "Dataflow as Datasource for Dataset");

      // Demo 5 - patch ADLS file datasource with Key credentials
     ProvisionDatasetWithAdlsFileDatasource(workspace, "ADLS Datasource - Contoso Sales", AppSettings.AdlsContainerPath, AppSettings.AdlsFileName1);
      ProvisionDatasetWithAdlsFileDatasource(workspace, "ADLS Datasource - Wingtip Sales", AppSettings.AdlsContainerPath, AppSettings.AdlsFileName2);

      // Demo 6 - patch SharePoint file datasource with OAuth2 credentials for User
      ProvisionDatasetWithSharePointFileDatasource(workspace, "SharePoint File Datasource - Customers");

      // Demo 7 - patch Kusto datasource with OAuth2 credentials for user and for service principal
      ProvisionDatasetWithKustoDatasource(workspace, "Kusto Datasource (User Access Token)", AppSettings.KustoServer, AppSettings.KustoDatabase, false);
      ProvisionDatasetWithKustoDatasource(workspace, "Kusto Datasource (Service Principal Access Token)", AppSettings.KustoServer, AppSettings.KustoDatabase, true);

    }

    public static void ProvisionDatasetWithAnonymousDatasource(Group Workspace, string ImportName) {
      Import importJob = ImportPBIX(Workspace.Id, Properties.Resources.DatasetWithAnonymousDatasource_pbix, ImportName);

      var dataset = GetDataset(Workspace.Id, ImportName);
      string datasetId = dataset.Id;

      PatchAnonymousDatasourceCredentials(Workspace.Id, datasetId);
      SetRefreshSchedule(Workspace.Id, datasetId);
      RefreshDataset(Workspace.Id, datasetId);
    }

    public static void ProvisionDatasetWithSqlDatasource(Group Workspace, string ImportName, string DatabaseServer, string DatabaseName) {

      // Import PBIX with dataset with Azure SQL datasource
      Import importJob = ImportPBIX(Workspace.Id, Properties.Resources.DatasetWithSqlDatasource_pbix, ImportName);

      // get DatasetId from Import object
      string datasetId = importJob.Datasets[0].Id;

      // update dataset parameters to point to correct Azure SQL database instance
      UpdateMashupParametersRequest req =
        new UpdateMashupParametersRequest(new List<UpdateMashupParameterDetails>() {
          new UpdateMashupParameterDetails { Name = "DatabaseServer", NewValue = DatabaseServer },
          new UpdateMashupParameterDetails { Name = "DatabaseName", NewValue = DatabaseName }
        });

      pbiClient.Datasets.UpdateParametersInGroup(Workspace.Id, datasetId, req);

      // Patch SQL Datasource Credentials after setting path to datasource
      PatchSqlDatasourceCredentials(Workspace.Id, datasetId, AppSettings.SqlUserName, AppSettings.SqlUserPassword);

      // Refresh Dataset and Set Refresh Schedule
      RefreshDataset(Workspace.Id, datasetId);
      SetRefreshSchedule(Workspace.Id, datasetId);
    }

    public static void ProvisionPaginatedReportWithSqlDatasource(Guid WorkspaceId, string ImportName) {

      var import = ImportRDL(WorkspaceId, Properties.Resources.CustomerSalesPaginated_rdl, ImportName);

      var report = pbiClient.Reports.GetReportInGroup(WorkspaceId, import.Reports[0].Id);

      var reportDatasources = pbiClient.Reports.GetDatasourcesInGroup(WorkspaceId, report.Id);
      Datasource reportDatasource = reportDatasources.Value[0];      

      // get dataset ID and gateway ID required to patch credentials
      var datasourceId = reportDatasource.DatasourceId;
      var gatewayId = reportDatasource.GatewayId;

      // Create UpdateDatasourceRequest to update Azure SQL datasource credentials
      UpdateDatasourceRequest req = new UpdateDatasourceRequest {
        CredentialDetails = new CredentialDetails(
          new BasicCredentials(AppSettings.SqlUserName, AppSettings.SqlUserPassword),
          PrivacyLevel.None,
          EncryptedConnection.NotEncrypted)
      };

      // Execute Patch command to update Azure SQL datasource credentials
      pbiClient.Gateways.UpdateDatasource((Guid)gatewayId, (Guid)datasourceId, req);
    }

    public static void ProvisionDatasetWithDataflowDatasource(Guid WorkspaceId, string ImportName) {

      // provisioning dataflows not supported for service principal
      if (CallAsUser == false) {
        pbiClient = TokenManager.GetPowerBiClientForUser();
      }

      // STEP 1 - create dataflow and patch credentials for its SQL datasources
      var dataflow = ImportDataflow(WorkspaceId, Properties.Resources.DataflowWithSQlDatasource_json);
      PatchSqlCredentialsForDatasourceBehindDataflow(WorkspaceId, dataflow);

      // STEP 2 - import PBIX with dataset which connects to dataflow
      var importDataset = ImportPBIX(WorkspaceId, Properties.Resources.DatasetWithDataflowDatasource_pbix, ImportName);
      var datasetId = importDataset.Datasets[0].Id;

      // STEP 3 - set dataset parameters to connect dataset to dataflow created in STEP 1
      UpdateMashupParametersRequest req =
        new UpdateMashupParametersRequest(new List<UpdateMashupParameterDetails>() {
          new UpdateMashupParameterDetails { Name = "TargetWorkspaceId", NewValue = WorkspaceId.ToString() },
          new UpdateMashupParameterDetails { Name = "TargetDataflowId", NewValue = dataflow.ObjectId.ToString() }
        });
      pbiClient.Datasets.UpdateParametersInGroup(WorkspaceId, datasetId, req);

      // STEP 4 - Patch credentials for dataset to connect to dataflow as it datasource
      PatchDataflowCredentials(WorkspaceId, datasetId);

      // Step 5 - refresh dataset
      RefreshDataset(WorkspaceId, datasetId);

      // revert back to calling as service principal
      if (CallAsUser == false) {
        pbiClient = TokenManager.GetPowerBiClientForServicePrincipal();
      }

    }

    public static void ProvisionDatasetWithAdlsFileDatasource(Group Workspace, string ImportName, string AdlsContainer, string ExcelFileName) {

      Import importJob = ImportPBIX(Workspace.Id, Properties.Resources.DatasetWithAdlsFileDatasource_pbix, ImportName);
      var dataset = GetDataset(Workspace.Id, ImportName);
      string datasetId = dataset.Id;

      var datasources = pbiClient.Datasets.GetDatasourcesInGroup(Workspace.Id, datasetId).Value;


      UpdateMashupParametersRequest req =
        new UpdateMashupParametersRequest(new List<UpdateMashupParameterDetails>() {
          new UpdateMashupParameterDetails { Name = "AdlsContainer", NewValue = AdlsContainer },
          new UpdateMashupParameterDetails { Name = "ExcelFileName", NewValue = ExcelFileName }
        });

      pbiClient.Datasets.UpdateParametersInGroup(Workspace.Id, datasetId, req);

      PatchAdlsCredentials(Workspace.Id, datasetId);

      RefreshDataset(Workspace.Id, datasetId);

    }

    public static void ProvisionDatasetWithSharePointFileDatasource(Group Workspace, string ImportName) {
      Import importJob = ImportPBIX(Workspace.Id, Properties.Resources.DatasetWithSharePointFileDatasource_pbix, ImportName);
      string datasetId = importJob.Datasets[0].Id;
      PatchSharePointListCredentials(Workspace.Id, datasetId);
      RefreshDataset(Workspace.Id, datasetId);
    }

    public static void ProvisionDatasetWithKustoDatasource(Group Workspace, string ImportName, string KustoServer, string KustoDatabase, bool UseServicePrincipalToken) {

      Import importJob = ImportPBIX(Workspace.Id, Properties.Resources.DatasetWithKustoDatasource_pbix, ImportName);
      string datasetId = importJob.Datasets[0].Id;

      UpdateMashupParametersRequest req =
        new UpdateMashupParametersRequest(new List<UpdateMashupParameterDetails>() {
          new UpdateMashupParameterDetails { Name = "KustoServer", NewValue = KustoServer },
          new UpdateMashupParameterDetails { Name = "KustoDatabase", NewValue = KustoDatabase }
        });

      pbiClient.Datasets.UpdateParametersInGroup(Workspace.Id, datasetId, req);

      PatchKustoDatabaseCredentials(Workspace.Id, datasetId, UseServicePrincipalToken);

      RefreshDataset(Workspace.Id, datasetId);
    }

    #endregion

    #region "Methods for Patching Datasource Credentials"

    public static void PatchAnonymousDatasourceCredentials(Guid WorkspaceId, string DatasetId) {

      // get datasources for dataset
      var datasources = pbiClient.Datasets.GetDatasourcesInGroup(WorkspaceId, DatasetId).Value;

      foreach (var datasource in datasources) {

        // check to ensure datasource use Web connector
        if (datasource.DatasourceType.ToLower() == "web") {

          // get DatasourceId and GatewayId
          var datasourceId = datasource.DatasourceId;
          var gatewayId = datasource.GatewayId;

          // Initialize UpdateDatasourceRequest object with AnonymousCredentials
          UpdateDatasourceRequest req = new UpdateDatasourceRequest {
            CredentialDetails = new CredentialDetails(
              new AnonymousCredentials(),
              PrivacyLevel.None,
              EncryptedConnection.NotEncrypted)
          };

          // Update datasource credentials through Gateways - UpdateDatasource
          pbiClient.Gateways.UpdateDatasource((Guid)gatewayId, (Guid)datasourceId, req);

        }
      }
      return;
    }

    public static void PatchSqlDatasourceCredentials(Guid WorkspaceId, string DatasetId, string SqlUserName, string SqlUserPassword) {

      var datasources = pbiClient.Datasets.GetDatasourcesInGroup(WorkspaceId, DatasetId).Value;

      // update credentials for all SQL datasources
      foreach (var datasource in datasources) {
        if (datasource.DatasourceType.ToLower() == "sql") {

          // get the datasourceId and the gatewayId
          var datasourceId = datasource.DatasourceId;
          var gatewayId = datasource.GatewayId;

          // Create UpdateDatasourceRequest using BasicCredentials
          UpdateDatasourceRequest req = new UpdateDatasourceRequest {
            CredentialDetails = new CredentialDetails(
              new BasicCredentials(SqlUserName, SqlUserPassword),
              PrivacyLevel.Organizational,
              EncryptedConnection.NotEncrypted)
          };

          // Execute Patch command to update Azure SQL datasource credentials
          pbiClient.Gateways.UpdateDatasource((Guid)gatewayId, (Guid)datasourceId, req);
        }
      };

    }

    public static void PatchSqlCredentialsForDatasourceBehindDataflow(Guid WorkspaceId, Dataflow TargetDataflow) {

      // get datasource behind dataflow
      var datasources = pbiClient.Dataflows.GetDataflowDataSources(WorkspaceId, TargetDataflow.ObjectId).Value;

      // find and patch credentials for SQL datasource
      foreach (var datasource in datasources) {
        if (datasource.DatasourceType.ToLower() == "sql") {
          // get the datasourceId and the gatewayId
          var datasourceId = datasource.DatasourceId;
          var gatewayId = datasource.GatewayId;
          // Create UpdateDatasourceRequest to update Azure SQL datasource credentials
          UpdateDatasourceRequest req = new UpdateDatasourceRequest {
            CredentialDetails = new CredentialDetails(
              new BasicCredentials(AppSettings.SqlUserName, AppSettings.SqlUserPassword),
              PrivacyLevel.None,
              EncryptedConnection.NotEncrypted)
          };
          // Execute Patch command to update Azure SQL datasource credentials
          pbiClient.Gateways.UpdateDatasource((Guid)gatewayId, (Guid)datasourceId, req);
        }
      };

      pbiClient.Dataflows.RefreshDataflow(WorkspaceId, TargetDataflow.ObjectId, new RefreshRequest { NotifyOption = "MailOnCompletion" });

    }

    public static void PatchDataflowCredentials(Guid WorkspaceId, string DatasetId) {

      var datasources = (pbiClient.Datasets.GetDatasourcesInGroup(WorkspaceId, DatasetId)).Value;

      // find the target SQL datasource
      foreach (var datasource in datasources) {
        if (datasource.DatasourceType.ToLower() == "extension") {
          // get the datasourceId and the gatewayId
          var datasourceId = datasource.DatasourceId;
          var gatewayId = datasource.GatewayId;

          // create credential details
          var CredentialDetails = new CredentialDetails();
          CredentialDetails.CredentialType = CredentialType.OAuth2;
          CredentialDetails.UseCallerAADIdentity = true;
          CredentialDetails.EncryptedConnection = EncryptedConnection.Encrypted;
          CredentialDetails.EncryptionAlgorithm = EncryptionAlgorithm.None;
          CredentialDetails.PrivacyLevel = PrivacyLevel.Organizational;

          // create UpdateDatasourceRequest 
          UpdateDatasourceRequest req = new UpdateDatasourceRequest(CredentialDetails);

          // set credentials          
          pbiClient.Gateways.UpdateDatasource((Guid)gatewayId, (Guid)datasourceId, req);

        }
      };

    }

    public static void PatchAdlsCredentials(Guid WorkspaceId, string DatasetId) {

      var datasources = pbiClient.Datasets.GetDatasourcesInGroup(WorkspaceId, DatasetId).Value;

      foreach (var datasource in datasources) {
        if (datasource.DatasourceType.ToLower() == "azuredatalakestorage") {

          // get the datasourceId and the gatewayId
          var datasourceId = datasource.DatasourceId;
          var gatewayId = datasource.GatewayId;

          // Create UpdateDatasourceRequest with KeyCredentials
          UpdateDatasourceRequest req = new UpdateDatasourceRequest {
            CredentialDetails = new CredentialDetails(
              new KeyCredentials(AppSettings.AdlsStorageKey),
              PrivacyLevel.None,
              EncryptedConnection.NotEncrypted)
          };

          // Execute Patch command to update ADLS credentials
          pbiClient.Gateways.UpdateDatasource((Guid)gatewayId, (Guid)datasourceId, req);
        }
      };
    }

    public static void PatchSharePointListCredentials(Guid WorkspaceId, string DatasetId) {

      // create scopes array to get access token for SharePoint site
      string[] SharePointSiteResourceUrl = new string[] {
        "https://powerbidevcamp.sharepoint.com/AllSites.Read"
      };

      // get access token for a specific user
      string accessTokenForSharePointSite = TokenManager.GetAccessToken(SharePointSiteResourceUrl);

      // use that access token to set OAuth credentials
      var datasources = (pbiClient.Datasets.GetDatasourcesInGroup(WorkspaceId, DatasetId)).Value;

      // find the target SQL datasource
      foreach (var datasource in datasources) {
        if (datasource.DatasourceType.ToLower() == "sharepointlist") {
          // get the datasourceId and the gatewayId
          var datasourceId = datasource.DatasourceId;
          var gatewayId = datasource.GatewayId;
          // Create UpdateDatasourceRequest to update Azure SQL datasource credentials
          UpdateDatasourceRequest req = new UpdateDatasourceRequest {
            CredentialDetails = new CredentialDetails(
              new OAuth2Credentials(accessTokenForSharePointSite),
              PrivacyLevel.None,
              EncryptedConnection.NotEncrypted)
          };
          // Execute Patch command to update Azure SQL datasource credentials
          pbiClient.Gateways.UpdateDatasource((Guid)gatewayId, (Guid)datasourceId, req);
        }
      };

    }

    public static void PatchKustoDatabaseCredentials(Guid WorkspaceId, string DatasetId, bool UseServicePrincipalToken) {

      var datasources = (pbiClient.Datasets.GetDatasourcesInGroup(WorkspaceId, DatasetId)).Value;

      // acquire access token for OAuth2 credentials
      string accessTokenForKustoServer;

      if (UseServicePrincipalToken) {
        // create scopes for service principal authentication
        string[] scopesForServicePrincipal = new string[] {
          AppSettings.KustoResourceId + "/.default"
        };
        // get access token for service principal
        accessTokenForKustoServer = TokenManager.GetAccessTokenForServicePrincipal(scopesForServicePrincipal);
      }
      else {
        // create scopes for user authentication
        string[] scopesForUser = new string[] {
          AppSettings.KustoResourceId + "/user_impersonation"
        };
        // get access token for user
        accessTokenForKustoServer = TokenManager.GetAccessToken(scopesForUser);
      }

      // Update Kusto Datasource using OAuth2 credentials
      foreach (var datasource in datasources) {
        if (datasource.DatasourceType.ToLower() == "extension") {
          // get the datasourceId and the gatewayId
          var datasourceId = datasource.DatasourceId;
          var gatewayId = datasource.GatewayId;
          // Create UpdateDatasourceRequest to update Azure SQL datasource credentials
          UpdateDatasourceRequest req = new UpdateDatasourceRequest {
            CredentialDetails = new CredentialDetails(
              new OAuth2Credentials(accessTokenForKustoServer),
              PrivacyLevel.None,
              EncryptedConnection.NotEncrypted)
          };
          // Execute Patch command to update Azure SQL datasource credentials
          pbiClient.Gateways.UpdateDatasource((Guid)gatewayId, (Guid)datasourceId, req);
        }
      };
    }

    #endregion

    #region "Methods for Creating Workspaces and Importing PBIX Files"

    public static Group CreatWorkspace(string Name) {
      Console.WriteLine("Creating workspace " + Name);

      // delete workspace with same name if it already exists
      Group workspace = GetWorkspace(Name);
      if (workspace != null) {
        pbiClient.Groups.DeleteGroup(workspace.Id);
        workspace = null;
      }

      // create new workspace
      GroupCreationRequest request = new GroupCreationRequest(Name);
      workspace = pbiClient.Groups.CreateGroup(request);
      return workspace;
    }

    public static void AssignWorkspaceToCapacity(Group Workspace) {

      // assign new workspace to dedicated capacity 
      if (AppSettings.PremiumCapacityId != "") {
        pbiClient.Groups.AssignToCapacity(Workspace.Id, new AssignToCapacityRequest {
          CapacityId = new Guid(AppSettings.PremiumCapacityId),
        });
      }
    }

    public static void AddDemoAdminUser(Group Workspace) {
      pbiClient.Groups.AddGroupUser(Workspace.Id, new GroupUser {
        Identifier = AppSettings.DemoAdminUser,
        PrincipalType = PrincipalType.User,
        EmailAddress = AppSettings.DemoAdminUser,
        GroupUserAccessRight = "Admin"
      });

    }

    public static Import ImportPBIX(Guid WorkspaceId, byte[] PbixContent, string ImportName) {
      Console.WriteLine("Importing PBIX for " + ImportName);

      MemoryStream stream = new MemoryStream(PbixContent);
      var import = pbiClient.Imports.PostImportWithFileInGroup(WorkspaceId, stream, ImportName, ImportConflictHandlerMode.CreateOrOverwrite);

      do { import = pbiClient.Imports.GetImportInGroup(WorkspaceId, import.Id); }
      while (import.ImportState.Equals("Publishing"));

      return import;
    }


    public static Import ImportPBIX2(Guid WorkspaceId, byte[] PbixContent, string ImportName) {
      Console.WriteLine("Importing " + ImportName);

      // load binary content from PBIX file into stream
      MemoryStream stream = new MemoryStream(PbixContent);

      // publish PBIX contents to Power BI using Post Import operation
      var import = pbiClient.Imports.PostImportWithFileInGroup(WorkspaceId,
                                                               stream,
                                                               ImportName,
                                                               ImportConflictHandlerMode.CreateOrOverwrite);

      // wait in loop until import operation completes
      while (import.ImportState == null || import.ImportState.Equals("Publishing")) {
        import = pbiClient.Imports.GetImportInGroup(WorkspaceId, import.Id);
        // wait a second before next call to check on status
        Thread.Sleep(1000);
      }

      // return Import object to caller
      return import;
    }

    public static void RefreshDataset(Guid WorkspaceId, string DatasetId) {
      pbiClient.Datasets.RefreshDatasetInGroup(WorkspaceId, DatasetId);
    }

    public static void SetRefreshSchedule(Guid WorkspaceId, string DatasetId) {

      var schedule = new RefreshSchedule {
        Enabled = true,
        Days = new List<Days?> {
          Days.Monday,
          Days.Tuesday,
          Days.Wednesday,
          Days.Thursday,
          Days.Friday
        },
        Times = new List<string> {
          "02:00",
          "11:30"
        },
        LocalTimeZoneId = "UTC",
        NotifyOption = CallAsUser ? ScheduleNotifyOption.MailOnFailure : ScheduleNotifyOption.NoNotification
      };

      pbiClient.Datasets.UpdateRefreshSchedule(WorkspaceId, DatasetId, schedule);

    }

    public static Import ImportRDL(Guid WorkspaceId, string RdlFileContent, string ImportName) {
      Console.WriteLine("Importing RDL for " + ImportName);

      string rdlImportName = ImportName + ".rdl";

      byte[] byteArray = Encoding.ASCII.GetBytes(RdlFileContent);
      MemoryStream RdlFileContentStream = new MemoryStream(byteArray);

      var import = pbiClient.Imports.PostImportWithFileInGroup(WorkspaceId,
                                                               RdlFileContentStream,
                                                               rdlImportName,
                                                               ImportConflictHandlerMode.Abort);

      // poll to determine when import operation has complete
      do { import = pbiClient.Imports.GetImportInGroup(WorkspaceId, import.Id); }
      while (import.ImportState.Equals("Publishing"));

      return import;

    }

    private static Dataflow ImportDataflow(Guid WorkspaceId, string DataflowDefinitionJson) {

      // CAUTION: Dataflow Import Not Supported for Service Principal

      byte[] byteArray = Encoding.ASCII.GetBytes(DataflowDefinitionJson);
      MemoryStream DataflowDefinitionStream = new MemoryStream(byteArray);

      // importing dataflow requires import name to be 'model.json'
      string ImportName = "model.json";

      // post import to start import process
      var import = pbiClient.Imports.PostImportWithFileInGroup(WorkspaceId,
                                                               DataflowDefinitionStream,
                                                               ImportName,
                                                               ImportConflictHandlerMode.GenerateUniqueName);

      // poll to determine when import operation has complete
      do { import = pbiClient.Imports.GetImportInGroup(WorkspaceId, import.Id); }
      while (import.ImportState.Equals("Publishing"));

      var dataflows = pbiClient.Dataflows.GetDataflows(WorkspaceId).Value;
      var dataflow = dataflows[0];

      // return Dataflow object to caller
      return dataflow;
    }

    #endregion

    #region "Basic Power BI REST API Helper Methods"

    public static void DisplayWorkspaces() {
      var workspaces = pbiClient.Groups.GetGroups().Value;
      if (workspaces.Count == 0) {
        Console.WriteLine("There are no workspaces for this user");
      }
      else {
        Console.WriteLine("WORKSPACES:");
        foreach (var workspace in workspaces) {
          Console.WriteLine("  " + workspace.Name + " - [" + workspace.Id + "]");
        }
      }
      Console.WriteLine();
    }

    public static Group GetWorkspace(string Name) {
      // build search filter with workspace name
      string filter = "name eq '" + Name + "'";
      var workspaces = pbiClient.Groups.GetGroups(filter: filter).Value;
      if (workspaces.Count == 0) {
        return null;
      }
      else {
        return workspaces.First();
      }
    }

    public static Dataset GetDataset(Guid WorkspaceId, string DatasetName) {
      var datasets = pbiClient.Datasets.GetDatasetsInGroup(WorkspaceId).Value;
      foreach (var dataset in datasets) {
        if (dataset.Name.Equals(DatasetName)) {
          return dataset;
        }
      }
      return null;
    }

    public static void GetDatasourcesForWorkspace(string WorkspaceName) {

      Console.WriteLine();
      Console.WriteLine("Generating JSON files for each datasource in workspace");

      Group workspace = GetWorkspace(WorkspaceName);
      var datasets = pbiClient.Datasets.GetDatasetsInGroup(workspace.Id).Value;
      foreach (var dataset in datasets) {
        var datasources = pbiClient.Datasets.GetDatasourcesInGroup(workspace.Id, dataset.Id).Value;
        SaveObjectAsJsonFile(dataset.Name + ".json", datasources);
      }

      var reports = pbiClient.Reports.GetReportsInGroup(workspace.Id).Value;
      foreach (var report in reports) {
        if (report.ReportType != "PowerBIReport") {
          var datasources = pbiClient.Reports.GetDatasourcesInGroup(workspace.Id, report.Id).Value;
          SaveObjectAsJsonFile(report.Name + ".json", datasources);
        }
      }

      var dataflows = pbiClient.Dataflows.GetDataflows(workspace.Id).Value;
      foreach (var dataflow in dataflows) {
        var datasources = pbiClient.Dataflows.GetDataflowDataSources(workspace.Id, dataflow.ObjectId).Value;
        SaveObjectAsJsonFile(dataflow.Name + ".json", datasources);
      }

    }

    #endregion

    #region "Methods for exporting Datasource objects to JSON"

    private static string ExportFolderPath = AppSettings.ExportToFilePath;

    private static void SaveObjectAsJsonFile(string FileName, object targetObject) {
      Console.WriteLine(" - Generating output file " + FileName);

      Stream exportFileStream = File.Create(ExportFolderPath + FileName);
      StreamWriter writer = new StreamWriter(exportFileStream);

      JsonSerializerSettings settings = new JsonSerializerSettings {
        DefaultValueHandling = DefaultValueHandling.Ignore,
        Formatting = Formatting.Indented
      };

      writer.Write(JsonConvert.SerializeObject(targetObject, settings));
      writer.Flush();
      writer.Close();
      exportFileStream.Close();

      // uncomment next line if you want the JSON file opened in Notepad
      // System.Diagnostics.Process.Start("notepad", ExportFolderPath + FileName);
    }

    #endregion

  }

}