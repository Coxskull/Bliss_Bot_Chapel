// Untrusted response contract. The server enforces authorization; client-side
// validation is defense in depth and prevents malformed UI/security states.
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const statuses=new Set(["OPEN","TRIAGED","IN_REVIEW","AWAITING_CUSTOMER","RESOLVED","CLOSED"]);
const roles=new Set(["GENERAL_SUPPORT","ADVERTISER_SUPPORT","CREATOR_SUPPORT",
  "BILLING_REVIEW","PRIVACY_REVIEW","SECURITY_REVIEW"]);
const categories=new Set(["general","advertiser","creator","billing","refund",
  "failed_campaign","privacy","security_incident"]);
export const isUuid=value=>typeof value==="string"&&uuid.test(value);
export function validCase(c){
  return c&&typeof c==="object"&&!Array.isArray(c)&&isUuid(c.case_id)
    &&isUuid(c.request_id)&&statuses.has(c.status)
    &&roles.has(c.assigned_role)&&categories.has(c.category)
    &&["NORMAL","HIGH","CRITICAL"].includes(c.severity)
    &&(c.owner_user_id==null||isUuid(c.owner_user_id));
}
export function parseQueue(data,requestedPage){
  if(!data||!Array.isArray(data.cases)||data.cases.length>20
    ||!data.cases.every(validCase)||data.page!==requestedPage||data.page_size!==20
    ||!Array.isArray(data.visible_roles)||!data.visible_roles.every(r=>roles.has(r))
    ||!data.cases.every(c=>data.visible_roles.includes(c.assigned_role)))
    throw Error("INVALID_OPERATOR_QUEUE");
  return {cases:data.cases,page:requestedPage,roles:data.visible_roles,
    hasNext:data.cases.length===20};
}
export function parseDetail(data,roleSet,expectedId){
  if(!data||!validCase(data.case)||data.case.case_id!==expectedId
    ||!roleSet.includes(data.case.assigned_role)||!data.contact
    ||typeof data.contact.email!=="string"
    ||typeof data.contact.full_name!=="string"
    ||typeof data.contact.description!=="string"
    ||!Array.isArray(data.events)||data.events.length>100)
    throw Error("INVALID_CASE_DETAIL");
  return data;
}
export function validateTransition(caseId,status,ownerId,resolution){
  const r=String(resolution??"");
  if(!isUuid(caseId)||!statuses.has(status)||!isUuid(ownerId)||r.length>1600
    ||(["RESOLVED","CLOSED"].includes(status)&&!r.trim()))
    throw Error("Invalid transition, owner or resolution.");
  return {case_id:caseId,status,owner_user_id:ownerId,resolution:r};
}
export function safeOperatorConfig(config,origin){
  if(!config||typeof config!=="object"
    ||config.supabaseUrl!=="https://bkutbglzivfdnoerigyb.supabase.co"
    ||typeof config.publishableKey!=="string"
    ||!(/^(sb_publishable_[A-Za-z0-9_-]+|eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+)$/).test(config.publishableKey)
    ||config.publishableKey.includes("REPLACE")
    ||!/^https:\/\/[^/]+$/.test(origin)
    ||config.operatorOrigin!==origin)
    throw Error("PRIVATE_OPERATOR_CONFIGURATION_NOT_VERIFIED");
  return config;
}
