using Microsoft.AspNetCore.Http;

namespace AssociationSaleTool.Web.Infrastructure;

public static class ReconciliationAdminSession
{
    private const string SessionKey = "ReconciliationAdminUnlocked";

    public static bool IsUnlocked(HttpContext httpContext)
    {
        return httpContext.Session.GetString(SessionKey) == "true";
    }

    public static void Unlock(HttpContext httpContext)
    {
        httpContext.Session.SetString(SessionKey, "true");
    }
}
