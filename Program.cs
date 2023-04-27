using SettingDatasourceCredentials.Services;

string WorkspaceName = "A Demo workspace";

DatasetManager.OnboardNewCustomerTenant(WorkspaceName);

DatasetManager.GetDatasourcesForWorkspace(WorkspaceName);


//OnPremGatewayManager.CreateGatewayDatasourceForLocalSqlServer();
//OnPremGatewayManager.CreateGatewayDatasourceForAzureSql();
//OnPremGatewayManager.CreateGatewayDatasourceForAdlsContainer();
//OnPremGatewayManager.GetGateways();