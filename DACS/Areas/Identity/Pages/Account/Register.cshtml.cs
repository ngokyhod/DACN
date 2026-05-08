// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using DACS.Models;
using DACS.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DACS.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly FirebaseSyncService _firebaseSync; // Đã khai báo
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly RoleManager<IdentityRole>_roleManager;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;


        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            ApplicationDbContext context,
            FirebaseSyncService firebaseSync)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
                _roleManager = roleManager; 
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
            _firebaseSync = firebaseSync;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            /// 

            [Required]
            public string FullName { get; set; }
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }
            [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
            [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
            [Display(Name = "Số điện thoại")]
            public string PhoneNumber { get; set; }
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            public string? Role { get; set; } // Danh sách các role
            [ValidateNever]
            public IEnumerable<SelectListItem> RoleList { get; set; } // Danh sách các role
        }

        
        public async Task OnGetAsync(string returnUrl = null)
        {

            if (!_roleManager.RoleExistsAsync(SD.Role_KhachHang).GetAwaiter().GetResult())
            {
                _roleManager.CreateAsync(new IdentityRole(SD.Role_KhachHang)).GetAwaiter().GetResult();
                _roleManager.CreateAsync(new IdentityRole(SD.Role_QuanLyXNK)).GetAwaiter().GetResult();
                _roleManager.CreateAsync(new IdentityRole(SD.Role_QuanLyND)).GetAwaiter().GetResult();
                _roleManager.CreateAsync(new IdentityRole(SD.Role_QuanLySP)).GetAwaiter().GetResult();
                _roleManager.CreateAsync(new IdentityRole(SD.Role_Owner)).GetAwaiter().GetResult();
                _roleManager.CreateAsync(new IdentityRole(SD.Role_KhachHang)).GetAwaiter().GetResult();
                _roleManager.CreateAsync(new IdentityRole(SD.Role_QuanLyDH)).GetAwaiter().GetResult();
            }

            Input = new()
            {
                RoleList = _roleManager.Roles.Select(x => x.Name).Select(i => new SelectListItem
                {
                    Text = i,
                    Value = i
                })
            };
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }
        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                var user = CreateUser();
                user.FullName = Input.FullName;
                user.PhoneNumber = Input.PhoneNumber;
                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    var roleToAssign = string.IsNullOrEmpty(Input.Role) ? SD.Role_KhachHang : Input.Role;
                    await _userManager.AddToRoleAsync(user, roleToAssign);
                    
                    
                    try
                    {
                        if (roleToAssign == SD.Role_KhachHang)
                        {
                            string sdtChoFirebase = null;
                            if (!string.IsNullOrEmpty(Input.PhoneNumber))
                            {
                                sdtChoFirebase = Input.PhoneNumber.Trim();
                                if (sdtChoFirebase.StartsWith("0"))
                                {
                                    sdtChoFirebase = "+84" + sdtChoFirebase.Substring(1);
                                }
                            }
                            
                            string firebaseUid = await _firebaseSync.CreateFirebaseUserAsync(
                                Input.Email,
                                Input.Password,
                                Input.FullName,
                                sdtChoFirebase
                            );
                            if (string.IsNullOrEmpty(firebaseUid))
                            {
                                await _userManager.DeleteAsync(user);

                                ModelState.AddModelError(string.Empty, "Đăng ký thất bại: Số điện thoại hoặc Email này đã tồn tại trên hệ thống Firebase.");
                                return Page(); 
                            }
                            var nguoiMuaProfile = new Models.KhachHang
                            {
                                M_KhachHang = Guid.NewGuid().ToString("N").Substring(0, 10),
                                
                                UserId = user.Id,
                                FirebaseID = firebaseUid,
                                Ten_KhachHang = user.FullName ?? user.UserName ?? "Chưa đặt tên",
                                Email_KhachHang = user.Email ?? "None",
                                SDT_KhachHang = Input.PhoneNumber,
                                MaTinh = "T00",
                                MaQuan = "Q0100",
                                MaXa = "X010100",
                                DiaChi_DuongApThon = "chua cap nhat",
                                LinhVucHoatDong = "null",
                                NhuCauChinh = "null",
                                TenDoanhNghiep = "null",
                                DiaChiDoanhNghiep = "null",
                                GiayPhepKinhDoanhUrl = "null",
                                TinhThanhDoanhNghiep = "null",
                                DiaChiDuongDoanhNghiep = "null",
                                EnterpriseLat = 0,
                                EnterpriseLng = 0,
                                MaQuanDoanhNghiep = null,
                                MaTinhDoanhNghiep = null,
                                MaXaDoanhNghiep = null
                            };
                            // DÙNG DbContext TRỰC TIẾP
                            _context.KhachHangs.Add(nguoiMuaProfile);
                            await _context.SaveChangesAsync(); // Lưu trực tiếp qua DbContext
                            _logger.LogInformation($"NguoiMua profile created for User ID: {user.Id}");
                            if (!string.IsNullOrEmpty(firebaseUid))
                            {
                                await _firebaseSync.SaveToFirestoreAsync(
                                    firebaseUid,
                                    Input.FullName,
                                    Input.Email,
                                    sdtChoFirebase
                                );
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error creating profile record (BenhNhan/NhanVien) for User ID: {user.Id}");

                    }
                    
                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                        $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                    $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }
}
