from pathlib import Path
import re
r=Path('ConsoleApp')
def w(path,s):
 p=r/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(s)
s=(r/'Terminal.cs').read_text()
helpers=s[s.index('    private static void ShowTickets'):s.rfind('}')].replace('private static','public static')
helpers=helpers.replace('        foreach (var a in details.AllocationHistory)', '''        if (details.Reservation is { } reservation)
        {
            var location = reservation.Equipment.Location;
            Console.WriteLine($"PICKUP: {location?.Name} | Building {location?.Building} | Floor {location?.Floor} | Ward {location?.Ward} | Room {location?.Room} | Shelf {location?.Shelf}");
            Console.WriteLine($"Collect by {reservation.Allocation.ReservationExpiresAt:u} (UTC); {Math.Max(0, reservation.RemainingSeconds):F0} seconds remaining. Use Mark picked up when collected.");
        }
        foreach (var a in details.AllocationHistory)''')
w(Path('Presentation/Shared/ConsoleInput.cs'),'using EquipmentManagementBackend.DTOs;\nnamespace CriticalCare.ConsoleApp;\npublic static class ConsoleInput\n{\n'+helpers+'}\n')
header='using EquipmentManagementBackend.DTOs;\nusing EquipmentManagementBackend.Models.Enums;\nusing static CriticalCare.ConsoleApp.ConsoleInput;\nnamespace CriticalCare.ConsoleApp;\n'
w(Path('Interfaces/Presentation/IRoleMenu.cs'),'namespace CriticalCare.ConsoleApp;\npublic interface IRoleMenu { string Role { get; } Task RunAsync(Session session); }\n')
w(Path('Presentation/Shared/MenuScreen.cs'),header+'''public abstract class MenuScreen(IAuthenticationService auth)
{
    protected async Task RunMenuAsync(Session session, string role, string title, Dictionary<string, (string Label, Func<Task> Run)> actions)
    {
        while (true)
        {
            var user = await auth.CurrentAsync(session);
            if (user.Role != role) throw new UnauthorizedAccessException("Your role changed; log in again.");
            Console.WriteLine($"\\n{user.Name} [{role}] - {title}");
            foreach (var action in actions) Console.WriteLine($"{action.Key} {action.Value.Label}");
            Console.WriteLine("0 Back / logout");
            var choice = Read("Choice");
            if (choice == "0") return;
            if (!actions.TryGetValue(choice, out var selected)) { Console.WriteLine("Choose an option from your menu."); continue; }
            try { await selected.Run(); }
            catch (EndOfStreamException) { throw; }
            catch (Exception error) { Console.WriteLine("Action not completed: " + error.Message); }
        }
    }
}
''')
# Extract existing case bodies as independent screen actions.
start=s.index('                    case "1":');end=s.index('                    default:',start)
blocks=re.split(r'                    case "(\d+)":',s[start:end]);cases={}
for i in range(1,len(blocks),2):
 body=blocks[i+1].strip()
 body=body.replace('case "23":','').strip()
 body=re.sub(r'break;\s*$','',body).strip()
 cases[blocks[i]]=body
cases['21']='''var id = Number("Ticket ID");
Console.WriteLine("APPROVAL RULES: assigned independent approver; pending approval; completed work report; at least one checklist item and all checks passed; calibration measurements recorded within stated limits. Review the evidence below before deciding.");
Console.WriteLine(await maintenance.DetailAsync(session, id));
await maintenance.ReviewAsync(session, id, (int)Number("Current version"), Yes("Approve? (n=reject)"), Read("Review notes"));'''
cases['41']='''var ward = Choice<WardType>("Ward type");
var type = Number("Equipment type ID");
var bed = Read("Bed number");
ShowBackup(await backups.RequestAsync(session, type, ward, bed, null));'''
cases['50']='await admin.DeleteEquipmentAsync(session, Number("Equipment ID to delete (history retained)")); Console.WriteLine("Equipment deleted from active inventory.");'
cases['51']='Console.WriteLine("Scheduled tickets created: " + await scheduling.GenerateAsync(session));'
labels={'1':'Equipment availability/filter','2':'Equipment types and locations','3':'Ticket logs','4':'Ticket details/history','10':'Equipment KPIs','11':'Exceptions and escalations','12':'Add/edit equipment and frequency schedules','13':'Employees','14':'Add/edit employee','15':'Add/edit location','16':'Add equipment type','17':'Create maintenance ticket','18':'Assign/reassign escalated ticket','19':'Audit logs','20':'Pending approvals','21':'Review evidence and approve/reject','22':'Close approved ticket','30':'Start/update assigned work','31':'Record checklist','32':'Record calibration measurements','33':'Submit for approval','34':'Equipment maintenance history','40':'Raise faulty equipment incident','41':'Request backup','42':'Allocation history','43':'Pickup location/deadline and details','44':'Retry reservation','45':'Mark picked up','46':'Mark returned','47':'Cancel request','50':'Delete equipment','51':'Generate due maintenance/calibration tickets'}
constructor='IAuthenticationService auth, IAdministrationService admin, IMaintenanceService maintenance, IReportsService reports, IBackupsService backups, ISchedulingService scheduling'
# Each concrete role only contains its own permitted actions.
for role,nums in [('Admin',[10,11,19,3,4,17,18,13,14,1,2,12,50,15,16,51]),('Technician',[3,4,30,31,32,33,34]),('Approver',[20,4,21,22]),('Incident',[1,2,40,3,4]),('Backup',[41,1,2,42,43,44,45,46,47])]:
 isrole=role in ['Admin','Technician','Approver']; folder=role if isrole else ('Staff' if role=='Incident' else 'Shared')
 className=role+('Menu' if isrole else 'Screen');roleval=role.lower() if isrole else 'staff'
 args=constructor if role=='Admin' else ('IAuthenticationService auth, IMaintenanceService maintenance, IReportsService reports' if role=='Incident' else 'IAuthenticationService auth, IBackupsService backups, IReportsService reports' if role=='Backup' else 'IAuthenticationService auth, IMaintenanceService maintenance')
 # Avoid unused constructor parameters.
 if role=='Admin':args+=', BackupScreen backupScreen'
 text=header+f'public sealed class {className}({args}) : MenuScreen(auth)'+(', IRoleMenu' if isrole else '')+'\n{\n'
 if isrole:text+=f'    public string Role => "{roleval}";\n'
 text+='    public Task RunAsync(Session session'+(', string role' if role=='Backup' else '')+') => RunMenuAsync(session, '+('role' if role=='Backup' else '"'+roleval+'"')+', "'+role+'", new()\n    {\n'
 for n in nums:text+='        ["'+str(n)+'"] = ("'+labels[str(n)]+'", async () => { '+cases[str(n)]+' }),\n'
 if role=='Admin':text+='        ["60"] = ("Backup allocation", () => backupScreen.RunAsync(session, "admin")),\n'
 text+='    });\n'
 if role=='Admin':text+=s[s.index('    private async Task EquipmentAsync'):s.index('    private static void ShowTickets')]
 text+='}\n';w(Path('Presentation')/folder/(className+'.cs'),text)
w(Path('Presentation/Staff/StaffMenu.cs'),header+'''public sealed class StaffMenu(IAuthenticationService auth, IncidentScreen incidents, BackupScreen backups) : MenuScreen(auth), IRoleMenu
{
    public string Role => "staff";
    public Task RunAsync(Session session) => RunMenuAsync(session, Role, "Staff", new()
    {
        ["1"] = ("Faulty equipment incidents", () => incidents.RunAsync(session)),
        ["2"] = ("Backup allocation", () => backups.RunAsync(session, Role))
    });
}
''')
# Login shell contains no role-specific action switch and no EF operations.
run=s[s.index('    public async Task RunAsync()'):s.index('    private async Task MenuAsync')]
run=run.replace('        await using (var db = database.Open()) await DbSeeder.SeedAsync(db);\n','')
run=run.replace('try { await MenuAsync(session); }','try { var user = await auth.CurrentAsync(session); await menus.Single(x => x.Role == user.Role).RunAsync(session); }')
run=run.replace('            catch (EndOfStreamException) { return; }\n        }','            catch (EndOfStreamException) { return; }\n            catch (Exception error) { Console.WriteLine(error.Message); }\n        }')
w(Path('Presentation/Terminal.cs'),header+'public sealed class Terminal(IAuthenticationService auth, IReadOnlyList<IRoleMenu> menus)\n{\n'+run+'}\n');(r/'Terminal.cs').unlink()
p=r/'Repositories/EfRepository.cs';s=p.read_text().replace(': IRepository<T> where',': IRepository<T>, IAsyncEnumerable<T> where').replace('    public Type ElementType','    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => entities.AsAsyncEnumerable().GetAsyncEnumerator(cancellationToken);\n    public Type ElementType');p.write_text(s)
