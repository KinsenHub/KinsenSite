using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Models;
using Microsoft.AspNetCore.Identity;

[ApiController]
[Route("umbraco/api/[controller]")]
public class ProfileCustomerController : ControllerBase
{
    private readonly IMemberService _memberService;
    private readonly IMemberManager _memberManager;
    private readonly ILogger<ProfileCustomerController> _logger;

    public ProfileCustomerController(
        IMemberService memberService,
        IMemberManager memberManager,
        ILogger<ProfileCustomerController> logger)
    {
        _memberService = memberService;
        _memberManager = memberManager;
        _logger = logger;
    }

    [HttpPost("SaveProfile")]
    public async Task<IActionResult> SaveProfile(
        [FromForm] string firstName,
        [FromForm] string lastName,
        // [FromForm] string password,
        [FromForm] string phone,
        [FromForm] string afm,
        [FromForm] string company,
        [FromForm] string email)
    {
        // ✅ ΤΟ ΜΟΝΑΔΙΚΟ ΣΩΣΤΟ AUTH CHECK
        var loggedIn = await _memberManager.GetCurrentMemberAsync();

        if (loggedIn == null)
            return Unauthorized("No member logged in");

        var member = _memberService.GetByKey(loggedIn.Key);
        if (member == null)
            return Unauthorized("Member not found");

        member.SetValue("umbracoMemberLogin", firstName);   
        member.SetValue("lastName", lastName);              
        member.SetValue("umbracoMemberEmail", email);
        // member.SetValue("umbracoMemberPassword", password);  
        member.SetValue("phone", phone);                   
        member.SetValue("afm", afm);                        
        member.SetValue("companyName", company);       

        _memberService.Save(member);

        _logger.LogInformation("✔ MEMBER UPDATED SUCCESSFULLY");

        return Ok(new { success = true, message = "Οι αλλαγές αποθηκεύτηκαν με επιτυχία!" });
    }
}
