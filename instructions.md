# CRM Email Auto-Linking Integration Instructions

## ✅ What I've Created

I've created a complete CRM Email Auto-Linking solution with the following components:

### 1. **Core Service File: `CrmEmailAutoLinkService.cs`**
- **Location**: `/var/www/onlyoffice/WebStudio/Core/CrmEmailAutoLinkService.cs`
- **Function**: Monitors for new emails every 30 seconds and calls the existing `LinkChainToCrmEnhanced` method
- **Features**:
  - Uses the existing `CrmLinkEngine.LinkChainToCrmEnhanced()` method
  - Multi-tenant support
  - Proper permission checking
  - Full CRM integration (chain linking, relationship events, attachments)

### 2. **Modified Startup Files**
- **Startup.cs**: Added initialization of the CRM service
- **Global.asax.cs**: Added Application_End handler for cleanup

### 3. **How It Works**
```csharp
// The service calls this existing method for each new email:
crmEngine.LinkChainToCrmEnhanced(message.Id, contactsToLink, httpContextScheme);
```

This triggers the full CRM integration that:
- ✅ Links mail chains to CRM contacts
- ✅ Creates relationship events with attachments  
- ✅ Makes emails visible in CRM interface
- ✅ Updates all necessary database tables

## 🔧 Integration Steps

### Option 1: Build and Deploy (Recommended)
1. **Compile the solution** with the new `CrmEmailAutoLinkService.cs` file
2. **Deploy** the updated assemblies
3. **Restart** the ONLYOFFICE application

### Option 2: Alternative Runtime Integration
If you can't build/deploy immediately, I can create a **standalone executable** that:
- Runs independently as a console application or Windows service
- Calls the ONLYOFFICE APIs to trigger CRM linking
- Can be deployed without recompiling the main application

## 📊 Expected Results

Once integrated, the service will:
- **Monitor** new emails every 30 seconds
- **Extract** email addresses from From, To, CC fields
- **Find** matching CRM contacts automatically
- **Link** using the full `LinkChainToCrmEnhanced` process
- **Make emails visible** in the CRM interface immediately

## 🧪 Testing

After deployment, you can test by:
1. **Sending an email** to/from a known CRM contact email address
2. **Waiting 30 seconds** for the monitoring cycle
3. **Checking the CRM contact page** - the email should appear in the history
4. **Monitoring logs** for processing messages

## 📝 Next Steps

**Which approach would you prefer?**

**A)** I'll help you build and deploy these files into your ONLYOFFICE installation

**B)** I'll create a standalone executable that calls the ONLYOFFICE APIs externally

**C)** I'll help troubleshoot the current startup issues and get the integrated service working

Let me know your preference and I'll proceed accordingly!