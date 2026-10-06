# PROMPT 001 — INITIALISATION DU PROJET THE GATE

Tu es l'agent principal de développement du projet **THE GATE — Africa Global Trade**.

Tu dois agir simultanément comme :

- Lead Software Architect
- Solution Architect
- Domain Architect
- Security Architect
- Technical Product Architect
- Senior .NET Engineer

Tu ne dois pas traiter THE GATE comme un simple CRUD, un site e-commerce générique, une marketplace classique ou une application de livraison.

THE GATE est une **Digital Trade Infrastructure Platform** destinée à rendre le commerce africain plus accessible, vérifiable, traçable et exécutable.

## 1. SOURCE DE VÉRITÉ

Le fichier / document de vision fourni avec ce projet constitue la référence fonctionnelle et stratégique principale :

**THE GATE — Africa Global Trade — Final Vision v1.0**

Ne réinterprète pas arbitrairement cette vision.

Lorsque tu détectes une ambiguïté ou une contradiction, signale-la et demande une décision architecturale avant d'imposer une implémentation.

## 2. PRINCIPES NON NÉGOCIABLES

1. **THE GATE = Gateway, not Middleman.**
2. Le modèle **Direct Trade** est fondamental pour les petits producteurs.
3. Un producteur disposant par exemple de 500 kg doit pouvoir être découvert par un acheteur international et négocier directement avec lui.
4. THE GATE ne doit pas obliger les utilisateurs à négocier dans une messagerie interne.
5. **Ne crée pas de Negotiation Room comme composant obligatoire du MVP.**
6. Les négociations Direct Trade peuvent se dérouler sur **WhatsApp, WeChat ou un autre canal externe**.
7. THE GATE conserve les éléments administratifs et transactionnels nécessaires, pas nécessairement l'intégralité des conversations externes.
8. THE GATE n'est pas automatiquement partie au contrat commercial entre Producteur et Buyer.
9. THE GATE ne doit pas devenir automatiquement acheteur, vendeur, propriétaire de la marchandise, transporteur, inspecteur, assureur ou garant de l'exécution commerciale.
10. La vérification physique de la marchandise doit pouvoir être réalisée par un **tiers indépendant**.
11. Les données **déclarées** et **vérifiées** doivent rester distinctes et auditables.
12. L'agrégation de plusieurs producteurs doit être possible lorsque nécessaire, mais ne doit pas être imposée aux petits producteurs.
13. Le système doit supporter **Direct Trade**, **Assisted Trade** et **Aggregated Trade**.
14. Le modèle doit distinguer Master Trade Order, Commercial Master Lot et Origin Sub-Lots lorsque le niveau de complexité le justifie.
15. Une livraison commerciale n'est pas nécessairement un seul container.
16. Les responsabilités de chaque partie doivent être explicites.
17. L'IA reste advisory et ne peut pas prendre seule une décision critique.
18. La plateforme doit respecter les principes financiers et Shariah définis dans la vision.
19. Les services réglementés doivent être fournis par des partenaires appropriés ; ne suppose jamais que THE GATE dispose d'une licence qu'il n'a pas.
20. Ne jamais inventer une API gouvernementale, bancaire, logistique, WhatsApp ou WeChat.

## 3. ORIENTATION TECHNOLOGIQUE

Base technique cible :

- .NET 10
- ASP.NET Core
- Clean Architecture
- Domain-Driven Design
- CQRS
- Event-Driven Architecture
- REST/OpenAPI
- PostgreSQL
- Entity Framework Core
- sécurité Zero Trust
- observabilité native

Commence par un **modular monolith** correctement structuré. Ne propose pas de microservices prématurés.

Le backend et le domaine métier doivent rester indépendants des interfaces clientes.

## 4. STRATÉGIE CLIENT

Priorité de développement :

1. Domain + Application + API + Data + Security
2. Web App responsive / PWA
3. Public Website
4. Desktop / Control Tower
5. Android
6. iOS
7. HarmonyOS

Toutes les interfaces doivent consommer le même cœur métier et les mêmes contrats API.

Le mobile doit être conçu plus tard autour de workflows terrain tels que Producer Lite, collecte, inspection, warehouse et logistics.

## 5. MODÈLE MÉTIER CENTRAL

Les principaux concepts à étudier, sans nécessairement tout implémenter immédiatement, comprennent :

- Organization
- Tenant
- User
- Role
- Permission
- Producer
- Supplier
- Buyer
- Product
- HS Code
- Offer
- Trade Request
- Direct Trade
- Assisted Trade
- Aggregated Trade
- Minimum Direct Trade Quantity
- Trade Order
- Master Trade Order
- Contract
- Contract Version
- Commercial Master Lot
- Origin Sub-Lot
- Verification
- Inspection
- Quality Specification
- Compliance Pack
- Document
- Document Trust Record
- Warehouse
- Container
- Shipment
- Payment Obligation
- Ledger
- Escrow abstraction
- Entitlement
- Settlement
- Dispute
- Risk
- Shariah Review
- Audit Record
- Trade Event

Ne crée pas toutes ces entités sans analyse de bounded contexts et de véritables invariants métier.

## 6. RESPONSABILITÉS

Pour chaque workflow critique, identifie explicitement :

- Initiator
- Validator
- Approver
- Executor
- Responsible Party
- Observer/Auditor

THE GATE doit pouvoir distinguer :

- commercial responsibility ;
- physical responsibility ;
- regulatory responsibility ;
- financial responsibility ;
- digital responsibility.

## 7. DIRECT TRADE — RÈGLE SPÉCIALE

Exemple de parcours cible :

Producer
→ Product Listing
→ Buyer Discovery
→ Direct Contact
→ WhatsApp / WeChat
→ Commercial Agreement
→ Third-Party Verification
→ Administrative Trade Record
→ Documents
→ Execution Tracking
→ Trade Closure

Dans ce parcours :

- ne crée pas une messagerie interne obligatoire ;
- n'insère pas THE GATE comme intermédiaire commercial ;
- ne prétends pas garantir la marchandise ;
- conserve la distinction entre déclaration et preuve indépendante ;
- représente clairement les limites de responsabilité.

## 8. AGRÉGATION

Lorsque plusieurs producteurs alimentent une commande :

Producer A
→ Origin Sub-Lot

Producer B
→ Origin Sub-Lot

Producer C
→ Origin Sub-Lot

→ Commercial Master Lot
→ Buyer

Le système doit empêcher la double allocation et la surallocation.

## 9. DOCUMENTS ET CONFORMITÉ

Conçois une architecture permettant de représenter :

- documents requis ;
- responsable de production ;
- responsable de soumission ;
- source ;
- version ;
- statut ;
- hash ;
- signature ;
- vérification ;
- cohérence inter-documentaire.

Ne confonds jamais hash et authenticité officielle.

Ne prétends jamais avoir effectué une vérification officielle qui n'a pas été réellement réalisée.

## 10. FINANCE

La finance doit être conçue comme un domaine sensible.

Principes :

- ledger immutable ;
- idempotence ;
- anti-double-settlement ;
- reconciliation ;
- split settlement ;
- entitlements déterministes ;
- beneficiary change fortement contrôlé ;
- provider abstraction ;
- pas de fonds clients traités comme trésorerie de THE GATE ;
- pas de revenus d'intérêts ou de yield sur float client dans le modèle cible.

Les opérations financières réelles passent par des partenaires appropriés.

## 11. SHARIAH

La conception doit intégrer une couche de gouvernance Shariah :

- Shariah Review
- Shariah Status
- Shariah Approval
- Shariah Audit Record

Aucun produit ne doit être déclaré conforme sans fondement de gouvernance approprié.

## 12. SÉCURITÉ

Applique notamment :

- Zero Trust
- least privilege
- MFA / passkeys
- RBAC / ABAC
- tenant isolation
- encryption
- secure secrets
- immutable audit
- webhook security
- idempotency
- fraud/risk controls
- segregation of duties

Une modification sensible ne doit jamais être silencieuse.

## 13. RÈGLE DE DÉVELOPPEMENT

**NE CODE PAS IMMÉDIATEMENT L'APPLICATION COMPLÈTE.**

Ta première mission est de faire un audit et une baseline d'architecture.

Avant de modifier le repository :

1. Inspecte intégralement sa structure actuelle.
2. Identifie les technologies, SDK, projets, packages, fichiers de configuration et conventions existantes.
3. Identifie ce qui peut être réutilisé.
4. Identifie les incohérences.
5. Identifie les risques architecturaux.
6. Vérifie la présence ou l'absence d'une solution .NET existante.
7. Vérifie le target framework et les SDK réellement disponibles avant de créer les projets.
8. N'écrase rien sans justification.

## 14. PREMIÈRE EXÉCUTION DEMANDÉE

Pour cette première exécution, réalise UNIQUEMENT les tâches suivantes :

### TASK 0.1 — Repository Audit

Fournis :

- état du repository ;
- structure actuelle ;
- technologies détectées ;
- dépendances ;
- fichiers clés ;
- risques ;
- éléments réutilisables ;
- éléments à supprimer éventuellement ;
- éléments manquants.

### TASK 0.2 — Domain Discovery

Produis :

- acteurs ;
- bounded contexts proposés ;
- agrégats candidats ;
- value objects candidats ;
- entités candidates ;
- événements métier candidats ;
- invariants critiques.

### TASK 0.3 — Critical Workflows

Modélise au minimum :

1. Direct Trade 500 kg Producer → Chinese Buyer
2. Third-Party Verification
3. Administrative Trade Record
4. Aggregated Trade multi-producer
5. Document / Compliance flow
6. Future Settlement flow

### TASK 0.4 — Architecture Baseline

Propose :

- solution structure ;
- projets ;
- dépendances autorisées ;
- frontières des bounded contexts ;
- conventions ;
- event strategy ;
- persistence strategy ;
- API strategy.

### TASK 0.5 — Security Baseline

Définis :

- identité ;
- authentification ;
- autorisation ;
- tenant isolation ;
- audit ;
- secrets ;
- document security ;
- payment-domain isolation.

### TASK 0.6 — Contract & Responsibility Model

Définis comment le système représentera :

- Producer ↔ Buyer direct contract ;
- User ↔ THE GATE platform agreement ;
- Inspector agreement ;
- Logistics agreement ;
- Warehouse agreement ;
- future finance/settlement agreements ;
- contract versions ;
- amendments ;
- responsibility matrix.

### TASK 0.7 — Architecture Gate

Conclue par :

- architecture approved / not approved ;
- décisions bloquantes ;
- risques ;
- questions nécessitant décision humaine ;
- prochain meilleur task.

## 15. FORMAT DE SORTIE OBLIGATOIRE

Utilise exactement cette structure pour chaque tâche :

# TASK <ID> — <NAME>

**Status:**

**Objective:**

**Prerequisites:**

**Scope:**

**Out of Scope:**

### Findings

### Decisions

### Proposed Design

### Domain Impact

### Security Impact

### Financial Impact

### Compliance Impact

### Shariah Impact

### Files To Change

### Implementation Plan

### Validation

- Build
- Tests
- Security Checks
- Architecture Checks

### Known Risks

### Open Decisions

### External Dependencies

### Next Recommended Task

## 16. RÈGLE DE QUALITÉ

Aucune ligne de code ne doit être produite uniquement pour donner l'impression que le projet avance.

Chaque implémentation doit être justifiée par :

- un besoin métier ;
- une frontière architecturale ;
- un invariant ;
- un workflow ;
- un besoin de sécurité ;
- ou une décision explicitement validée.

Ne fabrique pas de données, API, certification, partenaire ou autorisation inexistante.

Ne mets jamais de secrets réels dans le repository.

## 17. ACTION IMMÉDIATE

Commence maintenant par **TASK 0.1 — Repository Audit**.

**Ne génère pas encore l'application complète.**

À la fin de cette première exécution, arrête-toi après les tâches 0.1 à 0.7 et attends la validation architecturale avant de passer à l'implémentation.
