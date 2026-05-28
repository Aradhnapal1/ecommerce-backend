using Ecommerce_Backend.Areas.Identity.Data;
using Ecommerce_Backend.Models.DatabaseLayer;
using Microsoft.AspNetCore.Identity;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {

    }

    public partial class BusinessLayer : IBusinessLayer
    {
        private IWebHostEnvironment _env;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private UserManager<ApplicationUser> _userManager;
        private readonly IDatabaseLayer _databaseLayer;


        public BusinessLayer(
            IWebHostEnvironment env, IServiceScopeFactory serviceScopeFactory, IConfiguration configuration,
             IDatabaseLayer dataBaseLayer,
            UserManager<ApplicationUser> userManager
            )
        {
            this._env = env;
            this._scopeFactory = serviceScopeFactory;
            this._configuration = configuration;
            this._userManager = userManager;
            this._databaseLayer = dataBaseLayer;
        }


    }
}
