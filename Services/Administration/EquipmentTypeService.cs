using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.Services;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Application;

public sealed class EquipmentTypeService : IEquipmentTypeService
{
    private readonly IApplicationRepository database;
    private readonly IAuthenticationService auth;

    public EquipmentTypeService(IApplicationRepository database, IAuthenticationService auth)
    {
        this.database = database;
        this.auth = auth;
    }

    public async Task<long> AddTypeAsync(Session actor, string code, string name)
    {
        InputValidation.RequireText(code, 80, "Type code");
        InputValidation.RequireText(name, 150, "Type name");
        await using var db = database.Open();
        await auth.RequireAsync(db, actor, "admin");
        var type = new EquipmentType
        {
            TypeCode = code.Trim(),
            TypeName = name.Trim(),
            CreatedBy = actor.UserId

        };
        db.EquipmentTypes.Add(type);
        await db.SaveChangesAsync();
        return type.Id;
    }
}
