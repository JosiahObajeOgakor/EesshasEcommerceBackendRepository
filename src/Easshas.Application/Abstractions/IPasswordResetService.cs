using System;
using System.Threading.Tasks;

namespace Easshas.Application.Abstractions
{
    public interface IPasswordResetService
    {
        Task<bool> SendResetOtpAsync(string email);
        Task<bool> VerifyOtpAndResetPasswordAsync(string email, string otp, string newPassword);
    }
}
