# soil-sensor-monitoring
# Multi-Site Smart Soil Monitoring System

Final-year Work-Integrated Learning project · ITC327W · 2026
Central University of Technology, Free State

## Overview

A multi-site soil monitoring system that collects readings from
fifteen 7-in-1 soil sensors deployed across three locations,
stores them in a shared backend, and presents them through a
mobile application for field monitoring and a web application
for management and analysis.

## Stack

- **Mobile:** Flutter
- **Web:** ASP.NET MVC
- **Backend:** Supabase (PostgreSQL, Authentication, Storage)
- **Data source:** Third-party sensor system publishing to Firebase
- **Schedule:** Microsoft Project

## Stakeholder

[Name of the organisation you're working with, once confirmed]

## Group members

- [Name] — [role, e.g. Facilitator / SRS compiler]
- [Name] — [role]
- ...

## Current stage

Phase 1 — Planning, Requirements and Feasibility

## Repository layout

- `/documentation` — SRS, feasibility report, meeting minutes
- `/mobile` — Flutter application (added Phase 2)
- `/web` — ASP.NET application (added Phase 2)
- `/ingestion` — Supabase Edge Function for Firebase → Supabase (added Phase 2)
- `/database` — SQL schema, migrations (added Phase 2)

## Confidentiality

No credentials, API keys, stakeholder contact details or personal
information are stored in this repository. All secrets are held
in environment variables outside version control.
