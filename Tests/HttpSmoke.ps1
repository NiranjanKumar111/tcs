param([string]$BaseUrl = 'http://127.0.0.1:5089')
$ErrorActionPreference = 'Stop'
# Run only against the isolated test API; this creates test equipment and requests.
$swagger = Invoke-RestMethod "$BaseUrl/swagger/v1/swagger.json"
if (@($swagger.paths.PSObject.Properties).Count -lt 20) { throw 'Missing Swagger routes' }
$locations = Invoke-RestMethod "$BaseUrl/api/reference/locations"
$warehouse = $locations | Where-Object isCentralWarehouse | Select-Object -First 1
$code = 'HTTP-' + [guid]::NewGuid().ToString('N')
$body = @{ equipmentCode = $code; name = 'HTTP Smoke Ventilator'; equipmentTypeId = 1; locationId = $warehouse.id; createdBy = 1 } | ConvertTo-Json
$equipment = Invoke-RestMethod "$BaseUrl/api/equipment" -Method Post -ContentType 'application/json' -Body $body
$list = Invoke-RestMethod "$BaseUrl/api/equipment?search=$code&availability=available&isCentralWarehouse=true&pageSize=1"
if ($list.totalCount -ne 1) { throw 'Equipment list failed' }
$body = @{ requestedWard = 'ICU'; requestedBy = 2; equipmentTypeId = 1; requestedEquipmentId = $equipment.equipment.id; bedNumber = 'HTTP-B1' } | ConvertTo-Json
$request = Invoke-RestMethod "$BaseUrl/api/backup-requests" -Method Post -ContentType 'application/json' -Body $body
if ($request.request.status -ne 'reserved') { throw 'Reservation failed' }
$detail = Invoke-RestMethod "$BaseUrl/api/backup-requests/$($request.request.id)/reservation"
if ($detail.reservation.allocation.id -ne $request.reservation.allocation.id) { throw 'Reservation details failed' }
$retry = Invoke-RestMethod "$BaseUrl/api/backup-requests/$($request.request.id)/reserve" -Method Post -ContentType 'application/json' -Body '{"performedBy":2}'
if ($retry.reservation.allocation.id -ne $request.reservation.allocation.id) { throw 'Reserve retry failed' }
$shortage = Invoke-RestMethod "$BaseUrl/api/backup-requests" -Method Post -ContentType 'application/json' -Body $body
if ($shortage.request.status -eq 'reserved') {
    if ($shortage.reservation.allocation.equipmentId -eq $request.reservation.allocation.equipmentId) { throw 'Equipment double allocated' }
} elseif ($shortage.request.status -ne 'escalated') { throw 'Expected alternative reservation or shortage escalation' }
$escalations = Invoke-RestMethod "$BaseUrl/api/backup-requests/escalations"
if ($shortage.request.status -eq 'escalated' -and $escalations.totalCount -lt 1) { throw 'Escalation list failed' }
$cancelled = Invoke-RestMethod "$BaseUrl/api/backup-requests/$($shortage.request.id)/cancel" -Method Post -ContentType 'application/json' -Body '{"performedBy":2,"reason":"Smoke cancellation"}'
if ($cancelled.request.status -ne 'cancelled') { throw 'Cancellation failed' }
$pickup = Invoke-RestMethod "$BaseUrl/api/backup-requests/$($request.request.id)/allocations/$($request.reservation.allocation.id)/pickup" -Method Post -ContentType 'application/json' -Body '{"performedBy":2}'
if ($pickup.request.status -ne 'in_use') { throw 'Pickup failed' }
$returned = Invoke-RestMethod "$BaseUrl/api/backup-requests/$($request.request.id)/allocations/$($request.reservation.allocation.id)/return" -Method Post -ContentType 'application/json' -Body '{"performedBy":2}'
if ($returned.request.status -ne 'returned') { throw 'Return failed' }
$body = @{ name = 'Edited smoke equipment'; equipmentTypeId = 1; locationId = $warehouse.id; isActive = $true; updatedBy = 1 } | ConvertTo-Json
$edited = Invoke-RestMethod "$BaseUrl/api/equipment/$($equipment.equipment.id)" -Method Put -ContentType 'application/json' -Body $body
if ($edited.equipment.name -ne 'Edited smoke equipment') { throw 'Equipment edit failed' }
Invoke-RestMethod "$BaseUrl/api/equipment/$($equipment.equipment.id)" -Method Delete | Out-Null
$badRequestStatus = 0
try { Invoke-RestMethod "$BaseUrl/api/equipment?pageSize=0" | Out-Null }
catch { $badRequestStatus = [int]$_.Exception.Response.StatusCode }
if ($badRequestStatus -ne 400) { throw 'Validation status failed' }
$notFoundStatus = 0
try { Invoke-RestMethod "$BaseUrl/api/equipment/999999" | Out-Null }
catch { $notFoundStatus = [int]$_.Exception.Response.StatusCode }
if ($notFoundStatus -ne 404) { throw 'Not found mapping failed' }
Write-Output 'HTTP smoke passed: Swagger, equipment CRUD/filter, reserve/retry, detail, escalation/cancellation, pickup/return, 400 and 404 responses.'
