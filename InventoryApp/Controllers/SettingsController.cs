using InventoryApp.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace InventoryApp.Controllers
{
    public class SettingsController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly AppDbContext _db;

        public SettingsController(IWebHostEnvironment env, AppDbContext db)
        {
            _env = env;
            _db = db;
        }

        public IActionResult Index()
        {
            string dbFileName = "inventory.db";
            string filepath = Path.Combine(_env.ContentRootPath, dbFileName);
            string lastBackupFile = Path.Combine(_env.ContentRootPath, "last_backup.txt");

            ViewBag.DbPath = filepath;

            if (System.IO.File.Exists(lastBackupFile))
            {
                ViewBag.LastBackup = System.IO.File.ReadAllText(lastBackupFile);
            }
            else
            {
                ViewBag.LastBackup = "Zatím nezálohováno";
            }

            return View();
        }

        public async Task<IActionResult> DownloadBackup()
        {
            try
            {
                string tempFileName = $"backup_{Guid.NewGuid()}.db";
                string tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);

                await _db.Database.ExecuteSqlRawAsync("VACUUM INTO '" + tempFilePath + "'");

                string lastBackupFile = Path.Combine(_env.ContentRootPath, "last_backup.txt");
                System.IO.File.WriteAllText(lastBackupFile, DateTime.Now.ToString("d. M. yyyy HH:mm:ss"));

                byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(tempFilePath);
                System.IO.File.Delete(tempFilePath);

                string downloadName = $"ElectroVentory_Backup_{DateTime.Now:yyyyMMdd_HHmm}.db";

                return File(fileBytes, "application/octet-stream", downloadName);
            }
            catch (Exception ex)
            {
                TempData["ToastError"] = $"Chyba při generování zálohy: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        public async Task<IActionResult> RestoreBackup(IFormFile backupFile)
        {
            if (backupFile == null || backupFile.Length == 0)
            {
                TempData["ToastError"] = "Nebyl vybrán žádný soubor k obnově.";
                return RedirectToAction(nameof(Index));
            }

            if (!backupFile.FileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ToastError"] = "Nahraný soubor musí být databáze ve formátu .db.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                string dbFileName = "inventory.db";
                string filepath = Path.Combine(_env.ContentRootPath, dbFileName);

                SqliteConnection.ClearAllPools();

                using (var stream = new FileStream(filepath, FileMode.Create))
                {
                    await backupFile.CopyToAsync(stream);
                }

                TempData["ToastSuccess"] = "Databáze byla úspěšně obnovena ze zálohy!";
            }
            catch (Exception ex)
            {
                TempData["ToastError"] = $"Chyba při obnově databáze: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}