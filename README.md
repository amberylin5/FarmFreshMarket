# FarmFreshMarket - Secure Authentication System 
 
![CodeQL Analysis](https://github.com/amberylin5/FarmFreshMarket/actions/workflows/codeql-analysis.yml/badge.svg) 
 
A comprehensive ASP.NET Core authentication system with enterprise-grade security features. 
 
## Security Features 
 
### ? Implemented 
- **Two-Factor Authentication** (Email & SMS) 
- **Password Policies**: 12+ chars, mixed case, numbers, special chars 
- **Account Lockout**: 1 minute after 3 failed attempts 
- **Session Management**: 30-minute timeout, multiple session detection 
- **Data Encryption**: AES-256 for sensitive data 
- **XSS Prevention**: HTML encoding on all user inputs 
- **SQL Injection Prevention**: Parameterized queries 
- **Audit Logging**: All security events logged 
- **reCAPTCHA v3**: Anti-bot protection 
- **Password History**: Prevents reuse of last 2 passwords 
- **Password Expiry**: 5 minutes of inactivity 
 
### ?? Security Analysis 
This project uses GitHub CodeQL for automated security analysis. Click the badge above to view the latest security scan results. 
