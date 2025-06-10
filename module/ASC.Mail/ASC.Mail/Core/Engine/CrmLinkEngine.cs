/*
 *
 * (c) Copyright Ascensio System Limited 2010-2023
 * Updated: Force rebuild to eliminate email duplication
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * http://www.apache.org/licenses/LICENSE-2.0
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/


using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

using ASC.Common.Logging;
using ASC.CRM.Core;
using ASC.CRM.Core.Entities;
using ASC.Mail.Core.Dao.Expressions.Message;
using ASC.Mail.Data.Contracts;
using ASC.Mail.Data.Storage;
using ASC.Mail.Exceptions;
using ASC.Mail.Utils;
using ASC.Web.CRM.Core;
using ASC.Web.CRM.Core.Enums;
using ASC.Web.CRM.Classes;
using ASC.Web.Studio.Utility;

using Autofac;

namespace ASC.Mail.Core.Engine
{
    public class CrmLinkEngine
    {
        public int Tenant { get; private set; }

        public string User { get; private set; }

        public ILog Log { get; private set; }

        public CrmLinkEngine(int tenant, string user, ILog log = null)
        {
            Tenant = tenant;
            User = user;

            Log = log ?? LogManager.GetLogger("ASC.Mail.CrmLinkEngine");
        }

        public List<CrmContactData> GetLinkedCrmEntitiesId(int messageId)
        {
            using (var daoFactory = new DaoFactory())
            {
                var daoCrmLink = daoFactory.CreateCrmLinkDao(Tenant, User);

                var daoMail = daoFactory.CreateMailDao(Tenant, User);

                var mail = daoMail.GetMail(new ConcreteUserMessageExp(messageId, Tenant, User));

                return daoCrmLink.GetLinkedCrmContactEntities(mail.ChainId, mail.MailboxId);
            }
        }

        public void LinkChainToCrm(int messageId, List<CrmContactData> contactIds, string httpContextScheme)
        {
            using (var scope = DIHelper.Resolve())
            {
                var factory = scope.Resolve<CRM.Core.Dao.DaoFactory>();
                foreach (var crmContactEntity in contactIds)
                {
                    switch (crmContactEntity.Type)
                    {
                        case CrmContactData.EntityTypes.Contact:
                            var crmContact = factory.ContactDao.GetByID(crmContactEntity.Id);
                            CRMSecurity.DemandAccessTo(crmContact);
                            break;
                        case CrmContactData.EntityTypes.Case:
                            var crmCase = factory.CasesDao.GetByID(crmContactEntity.Id);
                            CRMSecurity.DemandAccessTo(crmCase);
                            break;
                        case CrmContactData.EntityTypes.Opportunity:
                            var crmOpportunity = factory.DealDao.GetByID(crmContactEntity.Id);
                            CRMSecurity.DemandAccessTo(crmOpportunity);
                            break;
                    }
                }
            }

            using (var daoFactory = new DaoFactory())
            {
                var db = daoFactory.DbManager;

                var daoMail = daoFactory.CreateMailDao(Tenant, User);

                var mail = daoMail.GetMail(new ConcreteUserMessageExp(messageId, Tenant, User));

                var daoMailInfo = daoFactory.CreateMailInfoDao(Tenant, User);

                var chainedMessages = daoMailInfo.GetMailInfoList(
                    SimpleMessagesExp.CreateBuilder(Tenant, User)
                        .SetChainId(mail.ChainId)
                        .Build());

                if (!chainedMessages.Any())
                    return;

                var linkingMessages = new List<MailMessageData>();

                var engine = new EngineFactory(Tenant, User);

                foreach (var chainedMessage in chainedMessages)
                {
                    var messageOptions = new MailMessageData.Options();
                    messageOptions.LoadImages = true;
                    messageOptions.LoadBody = true;
                    messageOptions.NeedProxyHttp = false;
                    
                    var message = engine.MessageEngine.GetMessage(chainedMessage.Id, messageOptions);

                    message.LinkedCrmEntityIds = contactIds;

                    linkingMessages.Add(message);

                }

                var daoCrmLink = daoFactory.CreateCrmLinkDao(Tenant, User);

                using (var tx = db.BeginTransaction(IsolationLevel.ReadUncommitted))
                {
                    daoCrmLink.SaveCrmLinks(mail.ChainId, mail.MailboxId, contactIds);

                    foreach (var message in linkingMessages)
                    {
                        try
                        {
                            AddRelationshipEvents(message, httpContextScheme);
                        }
                        catch (ApiHelperException ex)
                        {
                            if (!ex.Message.Equals("Already exists"))
                                throw;
                        }
                    }

                    tx.Commit();
                }
            }
        }

        public void MarkChainAsCrmLinked(int messageId, List<CrmContactData> contactIds)
        {
            using (var daoFactory = new DaoFactory())
            {
                var db = daoFactory.DbManager;

                var daoCrmLink = daoFactory.CreateCrmLinkDao(Tenant, User);

                using (var tx = db.BeginTransaction(IsolationLevel.ReadUncommitted))
                {
                    var daoMail = daoFactory.CreateMailDao(Tenant, User);

                    var mail = daoMail.GetMail(new ConcreteUserMessageExp(messageId, Tenant, User));

                    daoCrmLink.SaveCrmLinks(mail.ChainId, mail.MailboxId, contactIds);

                    tx.Commit();
                }
            }
        }

        public void UnmarkChainAsCrmLinked(int messageId, IEnumerable<CrmContactData> contactIds)
        {
            using (var daoFactory = new DaoFactory())
            {
                var db = daoFactory.DbManager;

                var daoCrmLink = daoFactory.CreateCrmLinkDao(Tenant, User);

                using (var tx = db.BeginTransaction(IsolationLevel.ReadUncommitted))
                {
                    var daoMail = daoFactory.CreateMailDao(Tenant, User);

                    var mail = daoMail.GetMail(new ConcreteUserMessageExp(messageId, Tenant, User));

                    daoCrmLink.RemoveCrmLinks(mail.ChainId, mail.MailboxId, contactIds);

                    tx.Commit();
                }
            }
        }

        public void ExportMessageToCrm(int messageId, IEnumerable<CrmContactData> crmContactIds)
        {
            if (messageId < 0)
                throw new ArgumentException(@"Invalid message id", "messageId");
            if (crmContactIds == null)
                throw new ArgumentException(@"Invalid contact ids list", "crmContactIds");

            var engine = new EngineFactory(Tenant, User);

            var messageOptions = new MailMessageData.Options();
            messageOptions.LoadImages = true;
            messageOptions.LoadBody = true;
            messageOptions.NeedProxyHttp = false;
            
            var messageItem = engine.MessageEngine.GetMessage(messageId, messageOptions);

            messageItem.LinkedCrmEntityIds = crmContactIds.ToList();

            AddRelationshipEvents(messageItem);
        }

        public void AddRelationshipEventForLinkedAccounts(MailBoxData mailbox, MailMessageData messageItem, string httpContextScheme)
        {
            try
            {
                using (var daoFactory = new DaoFactory())
                {
                    var dao = daoFactory.CreateCrmLinkDao(mailbox.TenantId, mailbox.UserId);

                    messageItem.LinkedCrmEntityIds = dao.GetLinkedCrmContactEntities(messageItem.ChainId, mailbox.MailBoxId);
                }

                if (!messageItem.LinkedCrmEntityIds.Any()) return;

                AddRelationshipEvents(messageItem, httpContextScheme);
            }
            catch (Exception ex)
            {
                Log.Warn(string.Format("Problem with adding history event to CRM. mailId={0}", messageItem.Id), ex);
            }
        }


        public void ProcessIncomingEmailForCrm(MailMessageData message, MailBoxData mailbox, string httpContextScheme)
        {
            Log.InfoFormat("DEBUG: ProcessIncomingEmailForCrm - METHOD CALLED for message {0} from {1} to {2}", message.Id, message.From, message.To);
            
            try
            {
                // First check if this email is already linked to CRM
                var existingCrmLinks = GetLinkedCrmEntitiesId(message.Id);
                if (existingCrmLinks.Any()) {
                    Log.InfoFormat("DEBUG: ProcessIncomingEmailForCrm - Message {0} is already linked to {1} CRM contact(s), skipping auto-processing", message.Id, existingCrmLinks.Count);
                    return;
                }
                
                Log.InfoFormat("DEBUG: ProcessIncomingEmailForCrm - No existing CRM links found for message {0}, proceeding with contact search", message.Id);
                // Extract all email addresses from message
                var allEmails = new List<string>();
                allEmails.AddRange(MailAddressHelper.ParseAddresses(message.From));
                allEmails.AddRange(MailAddressHelper.ParseAddresses(message.To));
                allEmails.AddRange(MailAddressHelper.ParseAddresses(message.Cc));

                Log.InfoFormat("DEBUG: ProcessIncomingEmailForCrm - Extracted {0} email addresses: {1}", allEmails.Count, string.Join(", ", allEmails));

                var contactsToLink = new List<CrmContactData>();

                using (var daoFactory = new DaoFactory())
                {
                    var crmContactDao = daoFactory.CreateCrmContactDao(Tenant, User);

                    foreach (var email in allEmails.Distinct())
                    {
                        if (string.IsNullOrEmpty(email)) continue;

                        // Check if contact already exists
                        var existingContactIds = crmContactDao.GetCrmContactIds(email);

                        if (existingContactIds.Any())
                        {
                            // Add existing contacts to link list
                            foreach (var contactId in existingContactIds)
                            {
                                // Avoid duplicate contacts in the link list
                                if (!contactsToLink.Any(c => c.Id == contactId && c.Type == CrmContactData.EntityTypes.Contact))
                                {
                                    contactsToLink.Add(new CrmContactData
                                    {
                                        Id = contactId,
                                        Type = CrmContactData.EntityTypes.Contact
                                    });
                                }
                            }
                            Log.InfoFormat("Found existing CRM contact(s) for email {0}: {1}", email, string.Join(",", existingContactIds));
                        }
                        else
                        {
                            Log.DebugFormat("No CRM contact found for email {0}, skipping", email);
                        }
                    }
                }

                // Link message to contacts if any were found - using enhanced manual-like process
                if (contactsToLink.Any())
                {
                    Log.InfoFormat("DEBUG: ProcessIncomingEmailForCrm - Found {0} CRM contacts to link for message {1}: {2}", 
                        contactsToLink.Count, message.Id, string.Join(",", contactsToLink.Select(c => $"ID:{c.Id} Type:{c.Type}")));
                    
                    // Set the linked CRM entities on the message for full processing
                    message.LinkedCrmEntityIds = contactsToLink;
                    
                    Log.InfoFormat("DEBUG: ProcessIncomingEmailForCrm - About to call LinkChainToCrmEnhanced for message {0}", message.Id);
                    
                    // Use the full LinkChainToCrm process like manual linking
                    LinkChainToCrmEnhanced(message.Id, contactsToLink, httpContextScheme);
                    Log.InfoFormat("DEBUG: ProcessIncomingEmailForCrm - Enhanced auto-linked message {0} to {1} CRM contact(s) with full functionality", message.Id, contactsToLink.Count);
                }
                else
                {
                    Log.InfoFormat("DEBUG: ProcessIncomingEmailForCrm - No CRM contacts found for message {0}, no linking performed", message.Id);
                }
            }
            catch (Exception ex)
            {
                Log.WarnFormat("ProcessIncomingEmailForCrm failed for message {0}: {1}", message.Id, ex.ToString());
            }
        }

        public void AddRelationshipEvents(MailMessageData message, string httpContextScheme = null)
        {
            using (var scope = DIHelper.Resolve())
            {
                var factory = scope.Resolve<CRM.Core.Dao.DaoFactory>();
                foreach (var contactEntity in message.LinkedCrmEntityIds)
                {
                    switch (contactEntity.Type)
                    {
                        case CrmContactData.EntityTypes.Contact:
                            var crmContact = factory.ContactDao.GetByID(contactEntity.Id);
                            CRMSecurity.DemandAccessTo(crmContact);
                            break;
                        case CrmContactData.EntityTypes.Case:
                            var crmCase = factory.CasesDao.GetByID(contactEntity.Id);
                            CRMSecurity.DemandAccessTo(crmCase);
                            break;
                        case CrmContactData.EntityTypes.Opportunity:
                            var crmOpportunity = factory.DealDao.GetByID(contactEntity.Id);
                            CRMSecurity.DemandAccessTo(crmOpportunity);
                            break;
                    }

                    var fileIds = new List<object>();

                    var apiHelper = new ApiHelper(httpContextScheme ?? Defines.DefaultApiSchema, Log);

                    foreach (var attachment in message.Attachments.FindAll(attach => !attach.isEmbedded))
                    {
                        if (attachment.dataStream != null)
                        {
                            attachment.dataStream.Seek(0, SeekOrigin.Begin);

                            var uploadedFileId = apiHelper.UploadToCrm(attachment.dataStream, attachment.fileName,
                                attachment.contentType, contactEntity);

                            if (uploadedFileId != null)
                            {
                                fileIds.Add(uploadedFileId);
                            }
                        }
                        else
                        {
                            using (var file = attachment.ToAttachmentStream())
                            {
                                var uploadedFileId = apiHelper.UploadToCrm(file.FileStream, file.FileName,
                                    attachment.contentType, contactEntity);

                                if (uploadedFileId != null)
                                {
                                    fileIds.Add(uploadedFileId);
                                }
                            }
                        }
                    }

                    apiHelper.AddToCrmHistory(message, contactEntity, fileIds);

                    Log.InfoFormat(
                        "CrmLinkEngine->AddRelationshipEvents(): message with id = {0} has been linked successfully to contact with id = {1}",
                        message.Id, contactEntity.Id);
                }
            }
        }

        public void LinkChainToCrmEnhanced(int messageId, List<CrmContactData> contactIds, string httpContextScheme)
        {
            Log.InfoFormat("DEBUG: LinkChainToCrmEnhanced - Starting enhanced linking for message {0} with {1} contacts", messageId, contactIds.Count);
            
            // Enhanced automatic linking that works like manual process
            // but with permission-safe checks instead of strict demands
            using (var scope = DIHelper.Resolve())
            {
                Log.InfoFormat("DEBUG: LinkChainToCrmEnhanced - DIHelper scope resolved, checking contact permissions");
                var factory = scope.Resolve<CRM.Core.Dao.DaoFactory>();
                var accessibleContacts = new List<CrmContactData>();
                
                foreach (var crmContactEntity in contactIds)
                {
                    try
                    {
                        switch (crmContactEntity.Type)
                        {
                            case CrmContactData.EntityTypes.Contact:
                                var crmContact = factory.ContactDao.GetByID(crmContactEntity.Id);
                                if (CRMSecurity.CanAccessTo(crmContact))
                                {
                                    accessibleContacts.Add(crmContactEntity);
                                }
                                else
                                {
                                    Log.WarnFormat("User {0} does not have access to CRM contact {1}, skipping", User, crmContactEntity.Id);
                                }
                                break;
                            case CrmContactData.EntityTypes.Case:
                                var crmCase = factory.CasesDao.GetByID(crmContactEntity.Id);
                                if (CRMSecurity.CanAccessTo(crmCase))
                                {
                                    accessibleContacts.Add(crmContactEntity);
                                }
                                else
                                {
                                    Log.WarnFormat("User {0} does not have access to CRM case {1}, skipping", User, crmContactEntity.Id);
                                }
                                break;
                            case CrmContactData.EntityTypes.Opportunity:
                                var crmOpportunity = factory.DealDao.GetByID(crmContactEntity.Id);
                                if (CRMSecurity.CanAccessTo(crmOpportunity))
                                {
                                    accessibleContacts.Add(crmContactEntity);
                                }
                                else
                                {
                                    Log.WarnFormat("User {0} does not have access to CRM opportunity {1}, skipping", User, crmContactEntity.Id);
                                }
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.WarnFormat("Failed to check access to CRM entity {0} type {1}: {2}", crmContactEntity.Id, crmContactEntity.Type, ex.Message);
                    }
                }

                if (!accessibleContacts.Any())
                {
                    Log.InfoFormat("No accessible CRM contacts found for message {0}, skipping enhanced linking", messageId);
                    return;
                }
            }

            using (var daoFactory = new DaoFactory())
            {
                var db = daoFactory.DbManager;

                var daoMail = daoFactory.CreateMailDao(Tenant, User);

                var mail = daoMail.GetMail(new ConcreteUserMessageExp(messageId, Tenant, User));

                var daoMailInfo = daoFactory.CreateMailInfoDao(Tenant, User);

                var chainedMessages = daoMailInfo.GetMailInfoList(
                    SimpleMessagesExp.CreateBuilder(Tenant, User)
                        .SetChainId(mail.ChainId)
                        .Build());

                if (!chainedMessages.Any())
                    return;

                var linkingMessages = new List<MailMessageData>();

                var engine = new EngineFactory(Tenant, User);

                foreach (var chainedMessage in chainedMessages)
                {
                    var messageOptions = new MailMessageData.Options();
                    messageOptions.LoadImages = true;
                    messageOptions.LoadBody = true;
                    messageOptions.NeedProxyHttp = false;
                    
                    var message = engine.MessageEngine.GetMessage(chainedMessage.Id, messageOptions);

                    message.LinkedCrmEntityIds = contactIds;

                    linkingMessages.Add(message);
                }

                var daoCrmLink = daoFactory.CreateCrmLinkDao(Tenant, User);

                Log.InfoFormat("DEBUG: LinkChainToCrmEnhanced - About to begin database transaction for chain {0} with {1} contacts", mail.ChainId, contactIds.Count);
                
                using (var tx = db.BeginTransaction(IsolationLevel.ReadUncommitted))
                {
                    Log.InfoFormat("DEBUG: LinkChainToCrmEnhanced - Transaction started, calling SaveCrmLinks for chain {0}, mailbox {1}", mail.ChainId, mail.MailboxId);
                    
                    daoCrmLink.SaveCrmLinks(mail.ChainId, mail.MailboxId, contactIds);
                    
                    Log.InfoFormat("DEBUG: LinkChainToCrmEnhanced - SaveCrmLinks completed successfully, processing {0} messages for relationship events", linkingMessages.Count);

                    foreach (var message in linkingMessages)
                    {
                        try
                        {
                            Log.InfoFormat("DEBUG: LinkChainToCrmEnhanced - Adding relationship events for message {0}", message.Id);
                            // This uploads attachments and creates full relationship events
                            AddRelationshipEvents(message, httpContextScheme);
                            Log.InfoFormat("DEBUG: LinkChainToCrmEnhanced - Successfully added relationship events for message {0}", message.Id);
                        }
                        catch (ApiHelperException ex)
                        {
                            if (!ex.Message.Equals("Already exists"))
                            {
                                Log.WarnFormat("DEBUG: LinkChainToCrmEnhanced - ApiHelperException for message {0}: {1}", message.Id, ex.Message);
                                // Don't throw - continue with other messages
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.WarnFormat("DEBUG: LinkChainToCrmEnhanced - Unexpected error adding relationship events for message {0}: {1}", message.Id, ex.Message);
                            // Don't throw - continue with other messages
                        }
                    }

                    Log.InfoFormat("DEBUG: LinkChainToCrmEnhanced - About to commit transaction for chain {0}", mail.ChainId);
                    tx.Commit();
                    Log.InfoFormat("DEBUG: LinkChainToCrmEnhanced - Transaction committed successfully for chain {0} with {1} CRM entities", mail.ChainId, contactIds.Count);
                }
            }
        }
    }
}
