ASISTENTI WEB PËR SIGURIMIN E PRONËS

Kërkon .NET 10 SDK dhe një OPENAI_API_KEY në SERVER.

1. Hape PropertyInsuranceWeb.csproj në Visual Studio Community.
2. Vendose OPENAI_API_KEY si Environment Variable në kompjuterin/serverin ku ekzekutohet aplikacioni.
3. Run.
4. Hap adresën që jep ASP.NET (zakonisht https://localhost:xxxx).

PDF-ja është tashmë brenda Data/ dhe nuk ka upload nga përdoruesi.
Në startin e parë serveri e ngarkon PDF-në në OpenAI File Search dhe ruan ID-në në Data/vectorstore.id.
Më pas nuk e ngarkon përsëri.

Për publikim në internet, API key duhet të jetë vetëm në server/hosting, JO në JavaScript.
