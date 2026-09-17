using EquipmentManagementBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (!await db.Users.AnyAsync())
        {
            db.Users.AddRange(
                new User { Name = "Admin User", Email = "admin@example.com", Role = "admin" },
                new User { Name = "Technician One", Email = "tech1@example.com", Role = "technician" },
                new User { Name = "Approver One", Email = "approver1@example.com", Role = "approver" }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.EquipmentTypes.AnyAsync())
        {
            db.EquipmentTypes.AddRange(
                new EquipmentType { TypeCode = "MECH_VENT", TypeName = "Mechanical Ventilator" },
                new EquipmentType { TypeCode = "DEFIB", TypeName = "Defibrillator" },
                new EquipmentType { TypeCode = "PATIENT_MON", TypeName = "Patient Monitor" },
                new EquipmentType { TypeCode = "INFUSION_PUMP", TypeName = "Infusion Pump" },
                new EquipmentType { TypeCode = "SYRINGE_PUMP", TypeName = "Syringe Pump" },
                new EquipmentType { TypeCode = "DIALYSIS_MACHINE", TypeName = "Portable Dialysis Machine" },
                new EquipmentType { TypeCode = "SUCTION_MACHINE", TypeName = "Portable Suction Machine" }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.Locations.AnyAsync())
        {
            db.Locations.AddRange(
                new Location { Code = "ICU-01", Name = "ICU" },
                new Location { Code = "ER-01", Name = "Emergency Room" }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.Locations.AnyAsync(x => x.Code == "CENTRAL-WAREHOUSE"))
        {
            db.Locations.Add(new Location { Code = "CENTRAL-WAREHOUSE", Name = "Central Warehouse", IsCentralWarehouse = true });
            await db.SaveChangesAsync();
        }
    }
}
