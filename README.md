# nulab_cup

## CI/CD Build

```bash
Unity -quit -batchmode -projectPath . \
  -executeMethod CIBuilder.BuildProject \
  -witToken "$SECRET_WIT_CLIENT_TOKEN" \
  -witServerToken "$SECRET_WIT_SERVER_TOKEN"
```
