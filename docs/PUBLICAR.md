# Publicar NovaFolder

Guía para el mantenedor. NovaFolder se distribuye de dos formas con el mismo código:

| Canal | Paquete | Firma | Actualizaciones |
|---|---|---|---|
| **Microsoft Store** (recomendado) | `NovaFolder_X.Y.Z.0_x64.msix` | La pone Microsoft: sin aviso de SmartScreen | Las hace la Store |
| **GitHub Releases** | `NovaFolder-win-Setup.exe` | Sin firma (aviso de SmartScreen) | Las hace NovaFolder (Velopack) |

## Datos del producto en la Store

No son secretos: van dentro de cada paquete y se pueden ver en la ficha de la Store. Están en `packaging/store/Package.appxmanifest` y en `UpdateService`.

| Dato | Valor |
|---|---|
| Package/Identity/Name | `Panditax727.NovaFolder` |
| Package/Identity/Publisher | `CN=F779745A-34B2-4F98-9C18-130EACD9002B` |
| PublisherDisplayName | `Panditax727` |
| Store ID | `9P3KG5PCB5HD` |

## 1. Primera publicación (a mano, una sola vez)

La API de la Store solo puede **actualizar** una app que ya existe; la primera vez se hace desde Partner Center.

1. Genera el paquete: `.\scripts\empaquetar-store.ps1` → `artifacts\store\NovaFolder_1.0.0.0_x64.msix`.
2. En [Partner Center](https://partner.microsoft.com/dashboard) → NovaFolder → **Iniciar el envío**:
   - **Precios y disponibilidad**: gratis, mercados.
   - **Propiedades**: categoría *Productividad*. Declara que la app se inicia con Windows.
   - **Clasificación por edades**: rellena el cuestionario.
   - **Paquetes**: sube el `.msix`.
   - **Descripción de la ficha (español)**: descripción, capturas de pantalla y palabras clave.
3. Envía a certificación. Microsoft la revisa (normalmente de uno a pocos días).

Opcional, para adelantarte a la revisión: ejecuta el kit de certificación **como administrador**:

```powershell
& "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit\appcert.exe" test `
  -appxpackagepath .\artifacts\store\NovaFolder_1.0.0.0_x64.msix -reportoutputpath .\artifacts\store\informe.xml
```

## 2. Credenciales para publicar desde GitHub Actions

Con esto, cada tag `vX.Y.Z` envía la versión nueva a la Store sin entrar a Partner Center.

### Obtener las credenciales (Partner Center)

Los nombres de los menús pueden variar un poco:

1. **Configuración de la cuenta** (engranaje) → **Administración de usuarios** → **Aplicaciones de Microsoft Entra** (antes *Azure AD*).
   Si te pide asociar un inquilino (*tenant*) de Microsoft Entra, créalo o asócialo; es gratis.
2. **Agregar aplicación de Entra** → crea una nueva con el rol **Administrador** (o *Desarrollador*).
3. Abre la aplicación creada → **Agregar clave**. Anota:
   - **Id. de inquilino** (Tenant ID)
   - **Id. de cliente** (Client ID)
   - **Clave** (Client Secret): solo se muestra una vez
4. **Configuración de la cuenta** → **Información legal** / perfil → anota el **Id. del vendedor** (Seller ID).

### Guardarlas en GitHub (nunca en el código)

1. Repositorio → **Settings → Environments → New environment** → `produccion`.
2. En **Deployment branches and tags** → *Selected branches and tags* → agrega la regla de tag `v*`.
3. En **Required reviewers** → agrégate a ti. Cada publicación esperará tu aprobación.
4. En **Environment secrets**, crea:

| Secret | Valor |
|---|---|
| `MSSTORE_TENANT_ID` | Id. de inquilino |
| `MSSTORE_SELLER_ID` | Id. del vendedor |
| `MSSTORE_CLIENT_ID` | Id. de cliente |
| `MSSTORE_CLIENT_SECRET` | Clave |

Si el Environment no tiene estos secrets, el workflow igualmente genera el `.msix` y lo deja descargable, solo que no lo envía.

La clave de Entra caduca (tú eliges cuándo al crearla). Antes de que caduque, crea otra y actualiza `MSSTORE_CLIENT_SECRET`.

## 3. Publicar una versión

1. Anota los cambios en `CHANGELOG.md` y sube `<Version>` en `Directory.Build.props`.
2. Commit y tag con el mismo número:
   ```powershell
   git tag v1.1.0
   git push origin main v1.1.0
   ```
3. En GitHub → **Actions** → *Release* → **Review deployments** → aprueba `produccion`.
4. El workflow:
   - ejecuta las pruebas;
   - publica el Setup en GitHub Releases;
   - genera el `.msix` y lo envía a la Store (si están los secrets).
5. La versión de la Store pasa otra vez por certificación antes de llegar a los usuarios.

## Diferencias de la versión de la Store

- **Datos**: configuración, íconos y registros en la carpeta del paquete (`%LOCALAPPDATA%\Packages\Panditax727.NovaFolder_99tkhxxdq91b6\LocalState`). Se borran al desinstalar.
- **Accesos guardados** («Limpiar el Escritorio»): en `Documentos\NovaFolder\Accesos guardados`, para que **sobrevivan a una desinstalación**.
- **Inicio con Windows**: tarea de inicio del paquete (se ve en Administrador de tareas → Aplicaciones de arranque).
- **Carpetas del Escritorio**: apuntan al alias `%LOCALAPPDATA%\Microsoft\WindowsApps\NovaFolder.exe`, que no cambia entre versiones.
- Quien pase del Setup a la Store conserva sus carpetas: la primera vez se copia su `folders.json`.
