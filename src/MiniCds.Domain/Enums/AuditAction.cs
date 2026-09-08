namespace MiniCds.Domain.Enums;

/// <summary>
/// All auditable operations (21 CFR Part 11 audit trail).
/// </summary>
public enum AuditAction
{
    CreateUser,
    ChangeMethod,
    StartAcquisition,
    StopAcquisition,
    VoidSample,
    ManualReintegrate,
    Sign,
    Approve,
    ExportReport,
    Login,
    FailedLogin
   }
