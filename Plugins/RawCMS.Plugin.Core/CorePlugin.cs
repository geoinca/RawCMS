﻿//******************************************************************************
// <copyright file="license.md" company="RawCMS project  (https://github.com/arduosoft/RawCMS)">
// Copyright (c) 2019 RawCMS project  (https://github.com/arduosoft/RawCMS)
// RawCMS project is released under GPL3 terms, see LICENSE file on repository root at  https://github.com/arduosoft/RawCMS .
// </copyright>
// <author>Daniele Fontani, Emanuele Bucarelli, Francesco Min�</author>
// <autogenerated>true</autogenerated>
//******************************************************************************
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using RawCMS.Library.Core;
using RawCMS.Library.Core.Extension;
using RawCMS.Library.DataModel;
using RawCMS.Library.Service;
using System;

namespace RawCMS.Plugins.Core
{
    public class CorePlugin : RawCMS.Library.Core.Extension.Plugin
    {
        public override string Name => "Core";

        public override string Description => "Add core CMS capabilities";

        public override void Init()
        {
            Logger.LogInformation("Core plugin loaded");
        }

        
        public override void OnApplicationStart()
        {

            base.OnApplicationStart();
        }

        //public override void SetAppEngine(AppEngine manager)
        //{
        //    base.SetAppEngine(manager);
        //}

        public override void ConfigureServices(IServiceCollection services)
        {
            

            services.AddOptions();

            Logger.LogInformation(configuration["MongoSettings:ConnectionString"]);


            var envConnectionString = Environment.GetEnvironmentVariable("MongoSettings:ConnectionString") ?? Environment.GetEnvironmentVariable("MongoSettingsConnectionString") ?? configuration["MongoSettings:ConnectionString"];
            var envDBName = Environment.GetEnvironmentVariable("MongoSettings:DBName") ?? Environment.GetEnvironmentVariable("MongoSettingsDBName")?? configuration["MongoSettings:DBName"];
           


            MongoSettings instance = new MongoSettings
            {
                ConnectionString = envConnectionString,
                DBName = envDBName
            };

            IOptions<MongoSettings> settingsOptions = Options.Create<MongoSettings>(instance);
            MongoService mongoService = new MongoService(settingsOptions, Logger);
            CRUDService crudService = new CRUDService(mongoService, settingsOptions);

            Engine.Service = crudService;

            services.AddSingleton<MongoService>(mongoService);
            services.AddSingleton<CRUDService>(crudService);
            services.AddSingleton<AppEngine>(Engine);
            services.AddHttpContextAccessor();

            crudService.EnsureCollection("_configuration");

            Engine.Plugins.ForEach(x => SetConfiguration(x, crudService));

            crudService.EnsureCollection("_schema");
        }

        private void SetConfiguration(Plugin plugin, CRUDService crudService)
        {
            Type confitf = plugin.GetType().GetInterface("IConfigurablePlugin`1");
            if (confitf != null)
            {
                Type confType = confitf.GetGenericArguments()[0];
                Type pluginType = plugin.GetType();

                ItemList confItem = crudService.Query("_configuration", new DataQuery()
                {
                    PageNumber = 1,
                    PageSize = 1,
                    RawQuery = @"{""plugin_name"":""" + pluginType.FullName + @"""}"
                });

                JObject confToSave = null;

                if (confItem.TotalCount == 0)
                {
                    confToSave = new JObject
                    {
                        ["plugin_name"] = plugin.GetType().FullName,
                        ["data"] = JToken.FromObject(pluginType.GetMethod("GetDefaultConfig").Invoke(plugin, new object[] { }))
                    };
                    crudService.Insert("_configuration", confToSave);
                }
                else
                {
                    confToSave = confItem.Items.First as JObject;
                }

                object objData = confToSave["data"].ToObject(confType);

                pluginType.GetMethod("SetActualConfig").Invoke(plugin, new object[] { objData });
            }
        }

        public override void Configure(IApplicationBuilder app, AppEngine appEngine)
        {
            
        }

        private IConfigurationRoot configuration;

        public override void Setup(IConfigurationRoot configuration)
        {
            this.configuration = configuration;
        }

        public override void ConfigureMvc(IMvcBuilder builder)
        {
        }
    }
}