using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using Microsoft.Data.SqlClient;      // For SQL Server
using MySql.Data.MySqlClient;        // For MySQL
using Npgsql;                        // For PostgreSQL
using Oracle.ManagedDataAccess.Client; // For Oracle

namespace DbConnectionTester.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult TestDbConnection(string dbType, string serverName, string dbName, string username, string password)
        {
            string connectionString = GetConnectionString(dbType, serverName, dbName, username, password);

            if (string.IsNullOrEmpty(connectionString))
            {
                ViewBag.Message = "Unsupported database type.";
                ViewBag.Success = false;
                return View("Index");
            }

            try
            {
                using (IDbConnection connection = GetDbConnection(dbType, connectionString))
                {
                    connection.Open();
                    ViewBag.Message = $"{dbType} database connection successful!";
                    ViewBag.Success = true;
                }
            }
            catch (Exception ex)
            {
                ViewBag.Message = $"Connection failed: {ex.Message}";
                ViewBag.Success = false;
            }

            return View("Index");
        }

        private string GetConnectionString(string dbType, string server, string database, string user, string password)
        {
            switch (dbType.ToLower())
            {
                case "sqlserver":
                    return $"Server={server};Database={database};User Id={user};Password={password};TrustServerCertificate=True;";
                case "mysql":
                    return $"Server={server};Database={database};User Id={user};Password={password};";
                case "postgresql":
                    return $"Host={server};Database={database};Username={user};Password={password};";
                case "oracle":
                    return $"Data Source={server};User Id={user};Password={password};";
                default:
                    return null;
            }
        }

        private IDbConnection GetDbConnection(string dbType, string connectionString)
        {
            return dbType.ToLower() switch
            {
                "sqlserver" => new SqlConnection(connectionString),
                "mysql" => new MySqlConnection(connectionString),
                "postgresql" => new NpgsqlConnection(connectionString),
                "oracle" => new OracleConnection(connectionString),
                _ => throw new NotSupportedException("Unsupported database type")
            };
        }
    }
}
