using Ecommerce_Backend.Areas.Identity.Data;
using Ecommerce_Backend.Services;
using Microsoft.AspNetCore.Identity;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial class DataBaseLayer : IDatabaseLayer
    {
        private readonly IConfiguration _configuration;
        private readonly string DbConnection;
        private readonly IRazorpayService _razorpayService;

        public DataBaseLayer(IConfiguration configuration, IEmailService emailService, IRazorpayService razorpayService)
        {
            this._configuration = configuration;
            this.DbConnection = this._configuration
                .GetConnectionString("AppDbContextConnection")!;
            this._emailService = emailService;
            this._razorpayService = razorpayService;
        }
    }
}