using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Extensions;
using UniVerein.Api.Models;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Serilog;

namespace UniVerein.Api.Controllers;

[Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.FINANCIAL_MANAGER)}")]
[ApiController]
[Route("acknowledgements")]
[EnableCors("AllowFrontend")]
public class AcknowledgementsController : ControllerBase
{
    private readonly AcknowledgementService _acknowledgementService;
    private readonly AcknowledgementPdfService _pdfService;
    private readonly AuditService _auditService;

    public AcknowledgementsController(AcknowledgementService acknowledgementService,
        AcknowledgementPdfService pdfService, AuditService auditService)
    {
        _acknowledgementService = acknowledgementService;
        _pdfService = pdfService;
        _auditService = auditService;
    }

    // Issues the payment acknowledgement on first call and returns the same document on every further call.
    [HttpPost]
    public async Task<IActionResult> IssueAsync([FromBody] AcknowledgementRequest request)
    {
        AcknowledgementIssueResult result = await _acknowledgementService.IssueAsync(request.Source, request.Id);

        if (result.Outcome == AcknowledgementOutcome.NOT_FOUND)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Income not found.",
                moreInfo: $"Income with ID {request.Id} not found."));

        if (result.Outcome == AcknowledgementOutcome.NOT_PAID)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "An acknowledgement can only be issued for paid incomes."));

        PaymentAcknowledgementEntity acknowledgement = result.Acknowledgement!;
        if (result.Outcome == AcknowledgementOutcome.ISSUED)
        {
            await _auditService.LogAsync(AuditLogActions.CREATE, nameof(PaymentAcknowledgementEntity),
                new { AcknowledgementId = acknowledgement.Id, acknowledgement.Number, request.Source, SourceId = request.Id });
            Log.Information("AcknowledgementsController: IssueAsync -> Acknowledgement {Number} issued by {ActorId}.",
                acknowledgement.Number, HttpContext.GetActorId());
        }

        return File(_pdfService.Render(acknowledgement), "application/pdf", $"Quittung-{acknowledgement.Number}.pdf");
    }
}
