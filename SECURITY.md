# Security Features in FarmFreshMarket 
 
## Implemented Security Measures 
 
### 1. Authentication & Authorization 
- Two-Factor Authentication (Email & SMS) 
- Password policies (12+ chars, mixed case, numbers, special chars) 
- Account lockout after 3 failed attempts (1 minute) 
- Session management with timeout 
- Role-based access control (Admin/User) 
 
### 2. Data Protection 
- Credit card encryption using AES-256 
- Password hashing via ASP.NET Identity 
- XSS prevention via HTML encoding 
- SQL injection prevention 
- HTTPS enforcement 
 
### 3. Security Monitoring 
- Audit logging of all user activities 
- Failed login tracking 
- Multiple session detection 
- Password change history (last 2 passwords) 
 
### 4. Compliance Features 
- reCAPTCHA v3 for anti-bot protection 
- Password expiry after 5 minutes of inactivity 
- Minimum 2 minutes between password changes 
- Secure password reset via email 
