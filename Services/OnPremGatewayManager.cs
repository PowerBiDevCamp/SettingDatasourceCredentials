using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Extensions;
using Microsoft.PowerBI.Api.Models;
using Microsoft.PowerBI.Api.Models.Credentials;
using Microsoft.Rest;

namespace SettingDatasourceCredentials.Services {

  public class OnPremGatewayManager {

    private static PowerBIClient pbiClient;
    private static bool CallAsUser = false;

    static OnPremGatewayManager() {
      if (CallAsUser) {
        pbiClient = TokenManager.GetPowerBiClientForUser(PowerBiPermissionScopes.OnPremGatewayManagement);
      }
      else {
        pbiClient = TokenManager.GetPowerBiClientForServicePrincipal();
      }
    }

    public static void GetGateways() {

      var gateways = pbiClient.Gateways.GetGateways().Value;

      foreach (var gateway in gateways) {
        Console.WriteLine("Gateway: " + gateway.Name);
        Console.WriteLine("Gateway Id: " + gateway.Id);
        Console.WriteLine();
        Console.WriteLine("Gateway datasources:");

        var datasources = pbiClient.Gateways.GetDatasources(gateway.Id).Value;

        foreach (var datasource in datasources) {
          Console.WriteLine();
          Console.WriteLine(" - Name: " + datasource.DatasourceName);
          Console.WriteLine(" - Id: " + datasource.Id);
          Console.WriteLine(" - DatasourceType: " + datasource.DatasourceType);
          Console.WriteLine(" - ConnectionDetails: " + datasource.ConnectionDetails);
          Console.WriteLine(" - CredentialType: " + datasource.CredentialType);
        }
        Console.WriteLine();
      }
    }

    public static void DeleteAllGatewayDatasources() {

      // Get Gateway objject
      Guid gatewayId = new Guid(AppSettings.OnPremGatewayId);
      var gateway = pbiClient.Gateways.GetGateway(gatewayId);

      var datasources = pbiClient.Gateways.GetDatasources(gateway.Id).Value;

      foreach (var datasource in datasources) {
        // delete gateway datasource
        pbiClient.Gateways.DeleteDatasource(gatewayId, datasource.Id);
      }

    }

    public static void CreateGatewayDatasourceForAzureSql() {

      // Get Gateway objject
      Guid gatewayId = new Guid(AppSettings.OnPremGatewayId);
      var gateway = pbiClient.Gateways.GetGateway(gatewayId);

      // configure datasource connection details
      string connectionDetails =
        JsonSerializer.Serialize(new {
          server = AppSettings.SqlDatabaseServer,
          database = AppSettings.SqlDatabaseWingtip
        });

      // create encryptor from Gateway's public key
      var credentialsEncryptor = new AsymmetricKeyEncryptor(gateway.PublicKey);

      // create credential details object with Basic Credentials
      var credentialDetails = new CredentialDetails(
        new BasicCredentials(username: AppSettings.SqlUserName, password: AppSettings.SqlUserPassword),
        PrivacyLevel.Private,
        EncryptedConnection.Encrypted,
        credentialsEncryptor);

      // create named datasource in On-Prem Gateway
      PublishDatasourceToGatewayRequest requestToAddDatasource = new PublishDatasourceToGatewayRequest {
        DataSourceName = "Wingtip Sales on DevCamp.Database.Windows.net",
        DataSourceType = "SQL",
        ConnectionDetails = connectionDetails,
        CredentialDetails = credentialDetails
      };

      GatewayDatasource datasource = pbiClient.Gateways.CreateDatasource(gatewayId, requestToAddDatasource);

      AddUserToGatewayDatasources(datasource, AppSettings.DemoAdminUser, DatasourceUserAccessRight.Read);

    }

    public static void CreateGatewayDatasourceForLocalSqlServer() {

      // Get Gateway objject
      Guid gatewayId = new Guid(AppSettings.OnPremGatewayId);
      var gateway = pbiClient.Gateways.GetGateway(gatewayId);

      // configure datasource connection details
      string connectionDetails =
        JsonSerializer.Serialize(new {
          server = AppSettings.LocalSqlServer,
          database = AppSettings.LocalSqlDatabase
        });

      // create encryptor from Gateway's public key
      var credentialsEncryptor = new AsymmetricKeyEncryptor(gateway.PublicKey);

      // create credential details object with Basic Credentials
      var credentialDetails = new CredentialDetails(
        new WindowsCredentials(username: AppSettings.WindowsUserName, password: AppSettings.WindowsUserPassword),
        PrivacyLevel.Private,
        EncryptedConnection.Encrypted,
        credentialsEncryptor);

      // create named datasource in On-Prem Gateway
      PublishDatasourceToGatewayRequest requestToAddDatasource = new PublishDatasourceToGatewayRequest {
        DataSourceName = "Wingtip Sales on On-prem SQL Server",
        DataSourceType = "SQL",
        ConnectionDetails = connectionDetails,
        CredentialDetails = credentialDetails
      };

      GatewayDatasource datasource = pbiClient.Gateways.CreateDatasource(gatewayId, requestToAddDatasource);

      AddUserToGatewayDatasources(datasource, AppSettings.DemoAdminUser, DatasourceUserAccessRight.Read);

    }

    public static void CreateGatewayDatasourceForAdlsContainer() {

      // Get Gateway objject
      Guid gatewayId = new Guid(AppSettings.OnPremGatewayId);
      var gateway = pbiClient.Gateways.GetGateway(gatewayId);

      // configure datasource connection details
      string connectionDetails =
        JsonSerializer.Serialize(new {
          server = AppSettings.AdlsStorageAccount,
          path = AppSettings.AdlsRelativeContainerPath
        });

      // create encryptor from Gateway's public key
      var credentialsEncryptor = new AsymmetricKeyEncryptor(gateway.PublicKey);

      // create credential details object with Basic Credentials
      var credentialDetails = new CredentialDetails(
        new KeyCredentials(AppSettings.AdlsStorageKey),
        PrivacyLevel.Private,
        EncryptedConnection.Encrypted,
        credentialsEncryptor);

      // create named datasource in On-Prem Gateway
      PublishDatasourceToGatewayRequest requestToAddDatasource = new PublishDatasourceToGatewayRequest {
        DataSourceName = "ADLS Container at " + AppSettings.AdlsContainerPath,
        DataSourceType = "AzureDataLakeStorage",
        ConnectionDetails = connectionDetails,
        CredentialDetails = credentialDetails
      };

      GatewayDatasource datasource = pbiClient.Gateways.CreateDatasource(gatewayId, requestToAddDatasource);

      AddUserToGatewayDatasources(datasource, AppSettings.DemoAdminUser, DatasourceUserAccessRight.Read);

    }

    public static void AddUserToGatewayDatasources(GatewayDatasource Datasource, string UserEmail, DatasourceUserAccessRight UserRights) {

      var datasource = pbiClient.Gateways.GetDatasource(Datasource.GatewayId, Datasource.Id);

      var datasourceUser = new DatasourceUser {
        PrincipalType = PrincipalType.User,
        EmailAddress = UserEmail,
        DatasourceAccessRight = UserRights
      };

      pbiClient.Gateways.AddDatasourceUser(Datasource.GatewayId, Datasource.Id, datasourceUser);
    }

    public static void AddServicePrincipalToGatewayDatasources(GatewayDatasource Datasource, string ServicePrincipalObjectId, DatasourceUserAccessRight UserRights) {

      var datasource = pbiClient.Gateways.GetDatasource(Datasource.GatewayId, Datasource.Id);

      var datasourceUser = new DatasourceUser {
        PrincipalType = PrincipalType.App,
        Identifier = ServicePrincipalObjectId,
        DatasourceAccessRight = UserRights
      };

      pbiClient.Gateways.AddDatasourceUser(Datasource.GatewayId, Datasource.Id, datasourceUser);
    }

    public static void BindDatasetToGatewayDatasource(Guid WorkspaceId, string DatasetId) {

      // Get Gateway objject
      Guid gatewayId = new Guid(AppSettings.OnPremGatewayId);
      var gateway = pbiClient.Gateways.GetGateway(gatewayId);

      // ensure caller is dataset owner
      pbiClient.Datasets.TakeOverInGroup(WorkspaceId, DatasetId);

      // bind dataset to gateway datasource
      pbiClient.Datasets.BindToGatewayInGroup(WorkspaceId, DatasetId, new BindToGatewayRequest(gateway.Id));
    }

  }
}

