PROPERTY INSURANCE WEB CHAT - SERVER VERSION

QELLIMI
Ky aplikacion eshte web chat informativ per sigurimin e prones. PDF-ja e Kushteve te Pergjithshme eshte e perfshire ne Data/.
Perdoruesi ne PC vetem hap web-in me Chrome/Edge. API key nuk vendoset ne PC-ne e perdoruesit.

SI FUNKSIONON
PC i punes -> Browser -> Serveri -> OpenAI API + File Search -> Pergjigjja

API KEY
OPENAI_API_KEY duhet te jete vetem ne SERVER/hosting, si environment variable ose secret.
Mos e vendos ne wwwroot, HTML, JavaScript ose ne PC-te e klienteve.

MODEL
Default: gpt-6-luna. Mund te ndryshohet me OPENAI_MODEL ne server.

VECTOR STORE
Pas nisjes se pare serveri e ngarkon PDF-ne ne OpenAI File Search dhe ruan ID-ne ne Data/vectorstore.id.
Ne hosting mund te vendoset edhe OPENAI_VECTOR_STORE_ID si secret/environment variable per ta bere ID-ne te qendrueshme pavaresisht deploy/restart.

DEPLOY ME DOCKER
1. Vendos projektin ne server/hosting me Docker.
2. Jep environment variables:
   OPENAI_API_KEY=...
   OPENAI_MODEL=gpt-6-luna
3. Nese ke vector store ID ekzistuese, vendos edhe:
   OPENAI_VECTOR_STORE_ID=vs_...
4. Publiko portin 8080.

DEPLOY NE AZURE APP SERVICE
- Krijo nje Azure App Service me Linux/Container ose .NET 10 sipas planit te hostimit.
- Te Configuration -> Environment variables vendos OPENAI_API_KEY.
- Mos e vendos key ne kod.
- Pasi web-i te jete online, perdor vetem URL-ne e web-it ne PC-te e punes.

TEST LOKAL
Per testim ne Visual Studio, OPENAI_API_KEY duhet te ekzistoje vetem ne makinen qe po ekzekuton serverin. Per perdorim nga PC-te e punes, serveri duhet te jete i hostuar.
