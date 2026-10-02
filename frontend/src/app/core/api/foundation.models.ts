export type FoundationPath =
  | 'OrdinaryMembership'
  | 'SupportGrant'
  | 'PlatformGovernanceContext'
  | string;

export interface FoundationSessionResponse {
  authenticated: boolean;
  actorId: string | null;
  sessionId: string | null;
  lifecycleState: string;
  absoluteExpiresAt: string | null;
  selectedPath: FoundationPath | null;
  selectedTenantId: string | null;
  selectedContextId: string | null;
  selectionVersion: number;
  displayName: string | null;
  login: string | null;
  replayed?: boolean;
  isEmergencySuperAdministrator?: boolean;
}

export interface FoundationEmergencyTenantsResponse {
  tenants: FoundationTenantCandidate[];
}

export type FoundationEntryMode = 'SignIn' | 'Tenant' | 'EmergencySuperAdministrator' | 'NoAccess';

export interface FoundationTenantCandidate {
  tenantId: string;
  displayName: string;
  arabicDisplayName?: string | null;
}

export interface FoundationOperationalContext {
  contextId: string;
  kind: 'Company' | 'Branch' | string;
  displayName: string;
  eligibilityVersion: number;
}

export interface FoundationBranding {
  displayName: string;
  logoLightUrl: string | null;
  logoDarkUrl: string | null;
  logoAltText: string;
  tenantConfigured: boolean;
  defaultTheme?: string | null;
  arabicDisplayName?: string | null;
}

export interface FoundationCurrencyPresentation {
  currencyCode: string;
  symbolAssetUrl: string | null;
  symbolTextFallback: string;
}

export interface FoundationEntryResponse {
  entryMode: FoundationEntryMode;
  operationalContexts: FoundationOperationalContext[];
  selectedOperationalContextId: string | null;
  operationalSelectionVersion: number;
  branding: FoundationBranding;
  currencyPresentation: FoundationCurrencyPresentation;
  code: string | null;
  isDevelopment: boolean;
  developmentAccountHint: string | null;
}

export interface FoundationOperationalContextsResponse {
  contexts: FoundationOperationalContext[];
  selectedContextId: string | null;
  selectionVersion: number;
}

export interface FoundationEmergencyTenantSwitchRequest {
  tenantId: string;
  expectedSelectionVersion: number;
}

export interface FoundationOperationalContextSwitchRequest {
  contextId: string;
  expectedSelectionVersion: number;
  expectedEligibilityVersion: number;
}

export interface FoundationOperationalContextSwitchResponse {
  selectedContext: FoundationOperationalContext;
  selectionVersion: number;
}

export interface FoundationProblemDetails {
  code?: string;
  correlationId?: string;
  operationId?: string;
}

export interface FoundationModuleDescriptor {
  module: string;
  name: string;
  boundary: string;
  registered: boolean;
}

export interface FoundationModuleRegistrationResponse extends FoundationModuleDescriptor {
  masterData: FoundationModuleDescriptor;
  businessParties: FoundationModuleDescriptor;
}
