using System.ComponentModel.DataAnnotations;

namespace AssociationSaleTool.Web.Services.Models;

public enum IntakeMode
{
    [Display(Name = "Enregistrement")]
    EmailPreregistration,

    [Display(Name = "Dépot")]
    InPerson
}
