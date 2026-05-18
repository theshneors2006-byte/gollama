# Dynamics 365 solution history client

דוגמת Console App ב-C# שמתחברת ל-Dynamics 365/Dataverse עם שם משתמש וסיסמה, שולחת את הבקשה:

```http
GET https://<your-dynamics-environment-url>/api/data/v9.0/msdyn_solutionhistories?$orderby=msdyn_starttime%20desc HTTP/1.1
```

ומדפיסה JSON מאוחד עם כל הרשומות שהוחזרו, כולל מעבר אוטומטי על `@odata.nextLink` אם Dataverse מחזיר כמה עמודים.

> שים לב: Dataverse Web API לא משתמש ב-Basic Auth. גם כשמזינים שם משתמש וסיסמה, צריך לקבל Azure AD access token ולהעביר אותו כ-`Bearer` token. הזרימה הזאת לא תעבוד עבור משתמשים שחייבים MFA או Conditional Access שחוסם username/password flow.

## הגדרות נדרשות

יש ליצור/להשתמש ב-App Registration שמאפשר Public client flow ושיש לו הרשאת Dynamics/Dataverse delegated permission מתאימה, למשל `user_impersonation`.

הגדר משתני סביבה לפני הרצה:

```bash
export D365_TENANT_ID="<azure-ad-tenant-id>"
export D365_CLIENT_ID="<public-client-app-registration-client-id>"
export D365_USERNAME="user@example.com"
export D365_PASSWORD="<password>"
export D365_ENVIRONMENT_URL="https://<your-dynamics-environment-url>"
```

אופציונלי, אם צריך scope שונה מברירת המחדל שמחושבת מתוך `D365_ENVIRONMENT_URL`:

```bash
export D365_SCOPES="https://<your-dynamics-environment-url>/user_impersonation"
```

## הרצה

```bash
dotnet restore examples/dynamics-solution-history/DynamicsSolutionHistoryClient.csproj
dotnet run --project examples/dynamics-solution-history/DynamicsSolutionHistoryClient.csproj
```

הפלט הוא JSON בפורמט יפה (`WriteIndented = true`) עם כל הערכים תחת השדה `value` ושדה נוסף `retrievedCount` שמציג כמה רשומות נאספו מכל העמודים.
