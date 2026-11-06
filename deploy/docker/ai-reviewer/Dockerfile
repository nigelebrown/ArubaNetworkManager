FROM python:3.12-slim
RUN groupadd -g 10001 reviewer && useradd -u 10001 -g 10001 -r -M -s /usr/sbin/nologin reviewer \
 && mkdir -p /reports /review/src /review/tests /review/input \
 && chown 10001:10001 /reports
COPY --chown=10001:10001 reviewer.py /opt/reviewer/reviewer.py
USER 10001:10001
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
WORKDIR /opt/reviewer
ENTRYPOINT ["python", "-u", "reviewer.py"]
