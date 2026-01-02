namespace AleaSim.Api.Models;

public record InjectBonusDto(decimal Amount, string Reason);
public record ForceCooldownDto(int DurationMinutes, string Reason);
public record SetRtpDto(decimal TargetRtp);
public record EmergencyStopDto(bool Enabled);
public record SetVolatilityDto(string Mode);
