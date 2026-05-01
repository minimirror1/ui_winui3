# Backend REST Smoke Test

## Environment

- BackendBaseUrl:
- Store ID:
- PC ID:
- Object ID:
- Test date:

## Checks

- [ ] Store detail 조회 성공: `GET /v1/service/stores/{store_id}/detail`
- [ ] 현재 `BackendPcId`와 일치하는 PC가 응답에 존재함
- [ ] PC metadata 갱신 성공: `PUT /v1/service/stores/{store_id}/pcs/{pc_id}`
- [ ] Object log 송신 성공: `POST /v1/service/objects/{object_id}/logs`
- [ ] `/logs` 송신 후 store detail의 `power_status` 반영 여부 확인
- [ ] `/v1/service/objects/{object_id}/power`가 호출되지 않았음

## Result

- Passed:
- Notes:
