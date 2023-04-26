using Microsoft.PowerBI.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SettingDatasourceCredentials {

  class AppSettings {

    // public Azure AD application metadata for user authentication
    public const string ApplicationId = "00000000-0000-0000-0000-000000000000";
    public const string RedirectUri = "http://localhost";

    // confidential Azure AD application metadata for service principal authentication
    public const string TenantId = "00000000-0000-0000-0000-000000000000";
    public const string ConfidentialApplicationId = "00000000-0000-0000-0000-000000000000";
    public const string ConfidentialApplicationSecret = "YOUR_APP_SECRET";
    public const string TenantSpecificAuthority = "https://login.microsoftonline.com/" + TenantId;

    // add Capacity Id for Premium capacity
    public const string PremiumCapacityId = "";

    // Admin user added to workspaces created by service principal
    public const string DemoAdminUser = "user1@MyPowerBiTenant.onmicrosoft.com";

    // SQL database server and database names
    public const string SqlDatabaseServer = "MyAzureSqlServer.database.windows.net";
    public const string SqlDatabaseWingtip = "Database1";
    public const string SqlDatabaseContoso = "Database2";

    // Basic authentication credentials for SQL datasources
    public const string SqlUserName = "User1";
    public const string SqlUserPassword = "";

    //// ADLS container and file info
    public const string AdlsStorageAccount = "https://mystorageaccount.dfs.core.windows.net";
    public const string AdlsContainerPath = "https://mystorageaccount.dfs.core.windows.net/exceldata/";
    public const string AdlsRelativeContainerPath = "/exceldata/";

    public const string AdlsFileName1 = "SalesDataProd.xlsx";
    public const string AdlsFileName2 = "SalesDataProd2.xlsx";

    // Key authentication credentials for ADLS
    public const string AdlsStorageKey = "MY_AZURE_STORAGE_KEY";

    // Kusto server and database names
    public const string KustoServer = "https://kustocluster1.eastus2.kusto.windows.net";
    public const string KustoDatabase = "KustoDB1";

    // Kusto resource ID used to acquire access token for OAuth2 credentials
    public const string KustoResourceId = "https://kusto.eastus2.kusto.windows.net";

    // On-Prem Gateway ID
    public const string OnPremGatewayId = "mystorageaccount";

    // Windows credentials for On-Prem Gateway datasources
    public const string WindowsUserName = @"DOMAIN1\User1";
    public const string WindowsUserPassword = "";

    // Local SQL Server and database names
    public const string LocalSqlServer = "LocalServer1";
    public const string LocalSqlDatabase = "WingtipSales";

    // local file path to export JSON files which let you inpsect datasource definitions
    public const string ExportToFilePath= @"C:\DevCamp\SettingDatasourceCredentials\Exports\";

  }

}
