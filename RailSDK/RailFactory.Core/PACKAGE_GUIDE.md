# 1. Modifica il codice in RailSDK/RailFactory.Core/

# 2. Incrementa versione in .csproj
<Version>1.0.1</Version>  # era 1.0.0

# 3. Rebuild package
cd C:\Work\Project\Alchemy\RailSDK\RailFactory.Core
dotnet pack --configuration Release --output .\nupkg

# 4. Update in AgentTest
cd C:\Work\Project\Alchemy\AgentTest
dotnet add package RailFactory.Core --version 1.0.1

# 5. Build
dotnet build


------------------------------------------------
# RailFactory.Core - Guida per Clienti

## 📦 Installazione Automatica (Consigliata)

### Opzione 1: NuGet Package Locale

**Dopo il build del progetto**, troverai il package in `RailSDK/nupkgs/`:
- `RailFactory.Core.1.0.0.nupkg`

#### Per il Cliente:

1. **Aggiungi source locale ai NuGet:**
```powershell
nuget sources add -Name "RailSDK-Local" -Source "C:\Path\To\RailSDK\nupkgs"
```

2. **Installa il package nel progetto:**
```powershell
dotnet add package RailFactory.Core --version 1.0.0
```

**✅ FATTO!** Tutte le dipendenze (Newtonsoft.Json, dnlib, System.Reflection.MetadataLoadContext) vengono installate automaticamente.

---

### Opzione 2: NuGet.org (Pubblico)

Se pubblichi su NuGet.org, il cliente fa semplicemente:

```powershell
dotnet add package RailFactory.Core
```

**Tutto automatico!** ✅

---

## 🏗️ Come Buildare il Package

### Build del Package

```powershell
cd C:\Work\Project\Alchemy\RailSDK\RailFactory.Core
dotnet build --configuration Release
```

**Output:** `RailSDK/nupkgs/RailFactory.Core.1.0.0.nupkg`

### Verifica il Package

```powershell
nuget verify RailSDK/nupkgs/RailFactory.Core.1.0.0.nupkg
```

---

## 📋 Cosa Include il Package

Quando il cliente installa `RailFactory.Core`, ottiene:

✅ **RailFactory.Core.dll**  
✅ **Newtonsoft.Json.dll** (13.0.4) - automatico  
✅ **dnlib.dll** (4.5.0) - automatico  
✅ **System.Reflection.MetadataLoadContext** (10.0.0) - automatico  

Tutte le DLL vengono copiate nella cartella `bin/` del progetto cliente **automaticamente**.

---

## 🎯 Esempio Uso Cliente

### 1. Installazione
```powershell
dotnet add package RailFactory.Core --version 1.0.0
```

### 2. Codice
```csharp
using RailFactory.Core;

// Nessuna configurazione necessaria!
var engine = new RailEngine(@"C:\Artifacts\MyTool");
engine.Load();
var result = engine.Execute("myFunction", "{ \"arg\": \"value\" }");
```

**Zero configurazione richiesta!** ✅

---

## 🔄 Aggiornamento Versione

Nel file `.csproj`, modifica:
```xml
<Version>1.1.0</Version>
```

Rebuild → nuovo package `RailFactory.Core.1.1.0.nupkg` generato automaticamente.

---

## 🌐 Pubblicazione su NuGet.org (Opzionale)

Se vuoi distribuire pubblicamente:

```powershell
# Get API key da https://www.nuget.org/
dotnet nuget push RailSDK/nupkgs/RailFactory.Core.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

Dopo la pubblicazione, chiunque può fare:
```powershell
dotnet add package RailFactory.Core
```

---

## ✅ Vantaggi

- ❌ **NO copia manuale** di Newtonsoft.Json.dll
- ❌ **NO configurazione** per il cliente
- ❌ **NO problemi** di dipendenze mancanti
- ✅ **TUTTO automatico** con NuGet

---

## 📝 Note

- **GeneratePackageOnBuild**: Package generato ad ogni build (Release)
- **PackageOutputPath**: I .nupkg vanno in `RailSDK/nupkgs/`
- **Dipendenze**: Dichiarate automaticamente nel package (da PackageReference)

Il cliente **non deve fare nulla** tranne installare il package! 🎉

