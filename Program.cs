using SettingDatasourceCredentials.Services;

string WorkspaceName = "AAA: Setting Credentials";

DatasetManager.OnboardNewCustomerTenant(WorkspaceName);

DatasetManager.GetDatasourcesForWorkspace(WorkspaceName);


//OnPremGatewayManager.CreateGatewayDatasourceForLocalSqlServer();
//OnPremGatewayManager.CreateGatewayDatasourceForAzureSql();
//OnPremGatewayManager.CreateGatewayDatasourceForAdlsContainer();
//OnPremGatewayManager.GetGateways();