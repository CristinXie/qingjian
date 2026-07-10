using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using QingJian.App.Data;
using QingJian.App.Services;
using QingJian.App.ViewModels;
using QingJian.App.Views;

namespace QingJian.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QingJian");
        Directory.CreateDirectory(appDataFolder);

        var databasePath = Path.Combine(appDataFolder, "qingjian.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var dbContext = new AppDbContext(options);
        var repository = new NoteRepository(dbContext);
        var service = new NoteService(repository);
        var viewModel = new MainViewModel(service);

        var window = new MainWindow(viewModel);
        window.Show();
    }
}
