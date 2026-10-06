import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';
import { API_BASE_URL } from './app/core/services/api-url.service';

interface RuntimeConfig {
  apiBaseUrl?: string;
}

fetch('/config.json', { cache: 'no-store' })
  .then(async response => {
    if (!response.ok) throw new Error(`Could not load config.json (${response.status})`);
    const config = await response.json() as RuntimeConfig;
    const apiBaseUrl = config.apiBaseUrl?.trim().replace(/\/+$/, '');
    if (!apiBaseUrl) throw new Error('config.json must define a non-empty apiBaseUrl.');

    return bootstrapApplication(AppComponent, {
      ...appConfig,
      providers: [
        ...(appConfig.providers ?? []),
        { provide: API_BASE_URL, useValue: apiBaseUrl }
      ]
    });
  })
  .catch((error: unknown) => {
    console.error('TradeBridge could not start because its runtime API configuration failed to load.', error);
  });
