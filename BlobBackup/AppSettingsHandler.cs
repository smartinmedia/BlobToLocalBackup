using BlobBackupLib.Azure.Model;
using BlobBackupLib.Jobs.Model;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace BlobBackupLib
{
    public class AppSettingsHandler
    {
        private string _filename;
        private AppSettings _config;

        public AppSettingsHandler(string filename)
        {
            _filename = filename;
            _config = GetAppSettings();
        }

        public AppSettings GetAppSettings()
        {
            var config = new ConfigurationBuilder()
               .SetBasePath(AppContext.BaseDirectory)
               .AddJsonFile(_filename, false, true)
               .Build();


            return config.GetSection("App").Get<AppSettings>();
        }

        public LoginCredentialsConfiguration GetLoginCredentials()
        {
            return new LoginCredentialsConfiguration()
            {
                ClientId = _config.LoginCredentials.ClientId,
                TenantId = _config.LoginCredentials.TenantId,
                ClientSecret = _config.LoginCredentials.ClientSecret
            };
        }

    }
}
