# Identity Access Service

## Vai tro

Xu ly dang nhap, phan quyen va quan ly tai khoan quan tri.

## Chuc nang

- login, logout, refresh token
- role/permission management
- audit truy cap
- federation/OAuth neu can

## Local hien tai

- JWT issuer cho admin dashboard
- seed san 2 tai khoan:
  - `admin.operator / Operator@123`
  - `admin.viewer / Viewer@123`
- du lieu luu trong MySQL:
  - `identity_access_users`
  - `identity_access_audit_logs`
