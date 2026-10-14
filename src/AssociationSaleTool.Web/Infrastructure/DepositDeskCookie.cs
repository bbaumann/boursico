namespace AssociationSaleTool.Web.Infrastructure;

/// <summary>
/// Identifies this computer's deposit desk. A cookie rather than a form field:
/// the deposit pages are plain form posts rendered by the server, so the desk
/// reaches every page and every save without client-side script.
/// </summary>
public static class DepositDeskCookie
{
    public const string Name = "poste-depot";

    public static string? Read(HttpRequest? request)
    {
        if (request is null)
        {
            return null;
        }

        return request.Cookies.TryGetValue(Name, out var token) ? token : null;
    }

    public static void Write(HttpResponse response, string token)
    {
        response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddDays(4)
        });
    }

    public static void Delete(HttpResponse response)
    {
        response.Cookies.Delete(Name);
    }
}
