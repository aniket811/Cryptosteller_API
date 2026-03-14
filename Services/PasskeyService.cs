using CryptostellerAPI.Models;
using CryptostellerAPI.Repository;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static CryptostellerAPI.Models.PasskeyDTOs;

namespace CryptostellerAPI.Services
{
    public class PasskeyService: IPasskeyService
    {
        private readonly IPasskeyRepository _repo;
        private readonly IDistributedCache _cache;
        private readonly ILogger<PasskeyService> _logger;
        private readonly IFido2 _fido2;
        private readonly Fido2NetLib.Fido2Configuration _fido2Config;
        public PasskeyService(IPasskeyRepository passkeyRepository, IDistributedCache cache, IFido2 fido2, ILogger<PasskeyService> logger, Fido2Configuration fido2Config)
        {
            _repo = passkeyRepository;
            _cache = cache;
            _fido2 = fido2;
            _logger = logger;
            _fido2Config = fido2Config;
        }
        public async Task<RegisterOptionsResponse> GenerateRegistrationOptionsAsync(
      string firebaseUid,
      string email,
      string displayName
      )
        {
            // 1. Build the Fido2 user object
            var fidoUser = new Fido2User
            {
                Id = Encoding.UTF8.GetBytes(firebaseUid),  // stored on device
                Name = email,
                DisplayName = displayName,
            };

            // 2. Fetch existing credentials → tell browser to exclude already-registered devices
            var existing = await _repo.GetActiveByUserIdAsync(firebaseUid);
            var excludeList = existing
                .Select(c => new PublicKeyCredentialDescriptor(Base64UrlDecode(c.CredentialId)))
                .ToList();
            // 3. Ask Fido2NetLib to generate creation options (includes the random challenge)
            var authenticatorSelection = new AuthenticatorSelection
            {
                AuthenticatorAttachment = AuthenticatorAttachment.Platform,  // device biometrics only
                ResidentKey = ResidentKeyRequirement.Required,   // passkey must be discoverable
                UserVerification = UserVerificationRequirement.Required,
            };

            var creationOptions = _fido2.RequestNewCredential(new()
            {
                User = fidoUser,
                ExcludeCredentials = excludeList,
                AuthenticatorSelection = authenticatorSelection,
                AttestationPreference = AttestationConveyancePreference.None,
            });

            // 4. Store the challenge in cache — retrieve it in Step 3 to verify
            var challengeId = Guid.NewGuid().ToString("N");
            await StoreChallengeAsync(challengeId, firebaseUid, creationOptions.Challenge, ChallengeType.Registration);

            // 5. Build response for Angular
            var excludeDtos = existing
                .Select(c => new ExcludeCredentialDto(c.CredentialId, "public-key"))
                .ToList();

            return new RegisterOptionsResponse
            {
                ChallengeId = challengeId,
                Challenge = Base64UrlEncode(creationOptions.Challenge),
                RpId = creationOptions.Rp.Id,
                RpName = creationOptions.Rp.Name,
                UserId = Base64UrlEncode(fidoUser.Id),
                UserName = email,
                DisplayName = displayName,
                ExcludeCredentials = excludeDtos,
            };
        }
        private async Task<(StoredChallenge? stored, string? error)> GetAndConsumeChallenge(string challengeId, ChallengeType expectedType)
        {
            if (string.IsNullOrEmpty(challengeId))
                return (null, "Missing challenge id.");

            var key = $"passkey:challenge:{challengeId}";
            var json = await _cache.GetStringAsync(key);
            if (string.IsNullOrEmpty(json))
                return (null, "Challenge not found or expired.");

            StoredChallenge? stored;
            try
            {
                stored = JsonSerializer.Deserialize<StoredChallenge>(json)!;
            }
            catch
            {
                return (null, "Invalid challenge data.");
            }

            if (stored is null)
                return (null, "Challenge not found.");

            if (stored.IsUsed)
                return (null, "Challenge already used.");

            if (stored.ExpiresAt < DateTime.UtcNow)
                return (null, "Challenge expired.");

            if (stored.Type != expectedType)
                return (null, "Challenge type mismatch.");

            // consume
            stored.IsUsed = true;
            await _cache.RemoveAsync(key);

            return (stored, null);
        }
        //Base 64 Helpers 
        private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        private static byte[] Base64UrlDecode(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            s += (s.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
            return Convert.FromBase64String(s);
        }

        private static T Fail<T>(string message) where T : class, new()
        {
            // Reflection-free: set Success=false and Message via known base
            if (typeof(T) == typeof(RegisterVerifyResponse))
                return (new RegisterVerifyResponse { Success = false, Message = message } as T)!;
            if (typeof(T) == typeof(AuthVerifyResponse))
                return (new AuthVerifyResponse { Success = false, Message = message } as T)!;
            throw new InvalidOperationException($"Fail<T> not configured for {typeof(T).Name}");
        }
        private async Task StoreChallengeAsync(
        string challengeId,
        string userId,
        byte[] challengeBytes,
        ChallengeType type)
        {
            var stored = new StoredChallenge
            {
                ChallengeId = challengeId,
                ChallengeBytes = Base64UrlEncode(challengeBytes),
                UserId = userId,
                Type = type,
                ExpiresAt = DateTime.UtcNow.AddSeconds(120),
            };

            await _cache.SetStringAsync(
                $"passkey:challenge:{challengeId}",
                JsonSerializer.Serialize(stored),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(120)
                });
        }

        public async Task<RegisterVerifyResponse> VerifyRegistrationAsync(
    string firebaseUid,
    string email,
    string displayName,
    RegisterVerifyRequest request)
        {
            var (stored, challengeError) = await GetAndConsumeChallenge(request.ChallengeId, ChallengeType.Registration);

            if (stored is null)
                return Fail<RegisterVerifyResponse>(challengeError!);

           
            try
            {
                var attestationResponse = new AuthenticatorAttestationRawResponse
                {
                    Id = request.Id,
                    RawId = Base64UrlDecode(request.RawId),
                    Type = PublicKeyCredentialType.PublicKey,
                    Response = new AuthenticatorAttestationRawResponse.AttestationResponse
                    {
                        AttestationObject = Base64UrlDecode(request.AttestationObject),
                        ClientDataJson = Base64UrlDecode(request.ClientDataJSON),
                    },
                };

                var result = await _fido2.MakeNewCredentialAsync(
                    new MakeNewCredentialParams
                    {
                        AttestationResponse = attestationResponse,
                        OriginalOptions = new CredentialCreateOptions
                        {
                            Challenge = Base64UrlDecode(stored.ChallengeBytes),
                            Rp = new PublicKeyCredentialRpEntity(
                                            _fido2Config.ServerDomain,
                                            _fido2Config.ServerName,
                                            null),
                            User = new Fido2User
                            {
                                Id = Encoding.UTF8.GetBytes(firebaseUid),
                                Name = email,
                                DisplayName = displayName,
                            },
                            PubKeyCredParams = new List<PubKeyCredParam>
                            {
                        new PubKeyCredParam(COSE.Algorithm.ES256),
                        new PubKeyCredParam(COSE.Algorithm.RS256),
                            },
                            AuthenticatorSelection = new AuthenticatorSelection
                            {
                                AuthenticatorAttachment = AuthenticatorAttachment.Platform,
                                ResidentKey = ResidentKeyRequirement.Required,
                                UserVerification = UserVerificationRequirement.Required,
                            },
                            Attestation = AttestationConveyancePreference.None,
                        },
                        IsCredentialIdUniqueToUserCallback = async (args, _) =>
                        {
                            var existing = await _repo.GetByCredentialIdAsync(
                                Base64UrlEncode(args.CredentialId));
                            return existing is null;
                        }
                    }
                );

                var credential = new PasskeyCredentialModel
                {
                    UserId = firebaseUid,
                    CredentialId = Base64UrlEncode(result.Id),
                    PublicKey = Base64UrlEncode(result.PublicKey),  // ← real COSE public key
                    SignCount = result.SignCount,
                    FriendlyName = request.FriendlyName ?? "My Passkey",
                    CreatedAt = DateTime.UtcNow,
                };

                await _repo.AddCredentialAsync(credential);

                _logger.LogInformation(
                    "Passkey registered. FirebaseUid={Uid} CredentialId={CredId}",
                    firebaseUid, credential.CredentialId);

                return new RegisterVerifyResponse
                {
                    Success = true,
                    CredentialId = credential.CredentialId,
                    Message = "Passkey registered successfully.",
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Passkey registration failed for {Uid}", firebaseUid);
                return Fail<RegisterVerifyResponse>(ex.Message);
            }
        }

        public async Task<AuthOptionsResponse> GenerateAuthOptionsAsync(AuthOptionsRequest request)
        {
            var allowCredentials = new List<AllowCredentialDto>();

            if (!string.IsNullOrEmpty(request.UserId))
            {
                var existing = await _repo.GetActiveByUserIdAsync(request.UserId);
                allowCredentials = existing
                    .Select(c => new AllowCredentialDto(c.CredentialId, "public-key"))
                    .ToList();
            }

            var challengeBytes = RandomNumberGenerator.GetBytes(32);
            var challengeId = Guid.NewGuid().ToString("N");

            await StoreChallengeAsync(
                challengeId,
                request.UserId ?? "anonymous",
                challengeBytes,
                ChallengeType.Authentication);

            return new AuthOptionsResponse
            {
                ChallengeId = challengeId,
                Challenge = Base64UrlEncode(challengeBytes),
                RpId = _fido2Config.ServerDomain,
                Timeout = 60000,
                AllowCredentials = allowCredentials,
            };
        }

        public async Task<AuthVerifyResponse> VerifyAuthenticationAsync(AuthVerifyRequest request)
        {
            var (stored, challengeError) = await GetAndConsumeChallenge(
                request.ChallengeId,
                ChallengeType.Authentication);

            _logger.LogInformation("Looking for CredentialId: {Id}", request.Id);

            if (stored is null)
                return new AuthVerifyResponse { Success = false, Message = challengeError };

            var credential = await _repo.GetByCredentialIdAsync(request.Id);

            if (credential is null)
            {
                var all = await _repo.GetActiveByUserIdAsync("gyLn3w3ZgSS19JSN1GE7RvHTjTr1");
                foreach (var c in all)
                    _logger.LogInformation("DB has CredentialId: {Id}", c.CredentialId);

                return new AuthVerifyResponse { Success = false, Message = "Passkey not found." };
            }

            try
            {
                var assertionResponse = new AuthenticatorAssertionRawResponse
                {
                    Id = request.Id,
                    RawId = Base64UrlDecode(request.RawId),
                    Type = PublicKeyCredentialType.PublicKey,
                    Response = new AuthenticatorAssertionRawResponse.AssertionResponse
                    {
                        AuthenticatorData = Base64UrlDecode(request.AuthenticatorData),
                        ClientDataJson = Base64UrlDecode(request.ClientDataJSON),
                        Signature = Base64UrlDecode(request.Signature),
                        UserHandle = request.UserHandle is not null
                                            ? Base64UrlDecode(request.UserHandle)
                                            : null,
                    },
                };
                var assertionParams = new MakeAssertionParams
                {
                    AssertionResponse = assertionResponse,
                    OriginalOptions = new AssertionOptions
                    {
                        Challenge = Base64UrlDecode(stored.ChallengeBytes),
                        RpId = _fido2Config.ServerDomain,
                        UserVerification = UserVerificationRequirement.Required,
                    },
                    StoredPublicKey = Base64UrlDecode(credential.PublicKey),  // ← was PublicKey
                    StoredSignatureCounter = (uint)credential.SignCount,        // ← cast long to uint 
                    IsUserHandleOwnerOfCredentialIdCallback = (args, _) => Task.FromResult(true)
                };

                var result = await _fido2.MakeAssertionAsync(assertionParams);

                await _repo.UpdateAfterLoginAsync(request.Id, (uint)result.SignCount);  // ← was result.Counter
                await _repo.UpdateAfterLoginAsync(request.Id, result.SignCount);

                return new AuthVerifyResponse
                {
                    Success = true,
                    CredentialId = credential.UserId,
                    Message = "Authentication successful.",
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Passkey authentication failed. CredentialId={CredId}", request.Id);
                return new AuthVerifyResponse { Success = false, Message = ex.Message };
            }
        }
    }
    }
