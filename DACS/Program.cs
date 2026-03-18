using System.Net;
using System.Net.Sockets;
using System.Text;
using DACS.Controllers;
using DACS.Models;
using DACS.Repositories;
using DACS.Repository;
using DACS.Services;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Nethereum.Contracts.Standards.ENS;
using Swashbuckle.AspNetCore.SwaggerGen;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddScoped<HomeController>();//kết nối chat
builder.Services.AddSingleton<SocketServer>();//kết nối chat
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
.AddDefaultTokenProviders()
.AddDefaultUI()
.AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews()
    .AddJsonOptions(x => x.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Giải quyết lỗi trùng tên Class (SchemaId already used)
    c.CustomSchemaIds(type => type.ToString());
});

builder.Services.AddControllers().AddJsonOptions(x =>
   x.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader());
});
builder.Services.ConfigureApplicationCookie(options => {
    options.LoginPath = $"/Identity/Account/Login";
    options.LogoutPath = $"/Identity/Account/Logout";
    options.LogoutPath = $"/Identity/Account/AccessDenied";
});


string path = Path.Combine(Directory.GetCurrentDirectory(), "firebase_config.json");
Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", path);

FirebaseApp.Create(new AppOptions()
{
    Credential = GoogleCredential.FromFile(path)
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
//builder.Services.AddScoped<IGeoService, GeoService>();
builder.Services.AddSingleton<BlockchainService>();
builder.Services.AddScoped<INguoiMuaRepository, NguoiMuaRepository>();
builder.Services.AddScoped<IThuGomRepository, ThuGomRepository>();
builder.Services.AddScoped<ISanPhamRepository, EFSanPhamRepository>();
builder.Services.AddScoped<ITonKhoRepository, TonKhoRepository>();
builder.Services.AddScoped<IPhieuXuatRepository, PhieuXuatRepository>();
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.Configure<ESmsSettings>(builder.Configuration.GetSection("ESmsSettings"));
builder.Services.AddTransient<ISmsService, ESmsService>();
builder.Services.AddScoped<FirebaseSyncService>();
builder.Services.AddHttpClient();
//builder.WebHost.UseUrls("http://0.0.0.0:5001");//chạy ebsite



var app = builder.Build();
var socketServer = app.Services.GetRequiredService<SocketServer>();
_ = Task.Run(() => socketServer.StartAsync()); //kết nối chat
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "Services", "AI_Engine", "KnowledgeBase")),
    RequestPath = "/pdfs" // Đặt tên ảo cho đường dẫn trên Web
});
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");
app.MapHub<ChatHub>("Hubs/ChatHub");
app.UseSession();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.MapControllers();
app.UseAuthorization();
app.MapRazorPages();

app.UseEndpoints(endpoints =>
{

    endpoints.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

    endpoints.MapControllerRoute(
        name: "Owner",
        pattern: "{area:exists}/{controller=Owner}/{action=Index}/{id?}");

    endpoints.MapControllerRoute(
        name: "KhachHang",
        pattern: "{area:exists}/{controller=KhachHang}/{action=Index}/{id?}");

    endpoints.MapControllerRoute(
        name: "QuanLyDH",
        pattern: "{area:exists}/{controller=QuanLyDH}/{action=Index}/{id?}");

    endpoints.MapControllerRoute(
        name: "QuanLyND",
        pattern: "{area:exists}/{controller=QuanLyND}/{action=Index}/{id?}");

    endpoints.MapControllerRoute(
    name: "QuanLyXNK",
    pattern: "{area:exists}/{controller=QuanLyXNK}/{action=Index}/{id?}");


    endpoints.MapControllerRoute(
name: "QuanLySP",
pattern: "{area:exists}/{controller=QuanLySP}/{action=Index}/{id?}");

    endpoints.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
});
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var blockchainService = services.GetRequiredService<BlockchainService>();

        // Log để biết nó đang chạy
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("Đang kích hoạt TestBlockchainAsync() khi khởi động...");

        // Gọi hàm (không cần await để không block khởi động)
        _ = blockchainService.TestBlockchainAsync();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Không thể chạy BlockchainService test khi khởi động.");
    }
}

app.Run();
