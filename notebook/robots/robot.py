#!/usr/bin/env python3
"""Stateless robot worker for notebook claim processing.

Pulls jobs from the notebook server's job queue, processes them
with a cheap LLM (Haiku-class), and pushes results back.

Usage:
    python robot.py --server http://localhost:5000 \
                    --notebook <uuid> \
                    --worker-id robot-haiku-1 \
                    --token <jwt-token> \
                    [--job-type DISTILL_CLAIMS] \
                    [--model claude-haiku-4-5-20251001] \
                    [--poll-interval 5]
"""

import argparse
import logging
import time
from typing import Optional

import anthropic
import requests

from prompts import (
    build_distill_prompt,
    build_compare_prompt,
    build_classify_prompt,
    parse_distill_result,
    parse_compare_result,
    parse_classify_result,
)

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s [%(name)s] %(message)s",
)
logger = logging.getLogger("robot")


def pull_job(
    session: requests.Session,
    server: str,
    notebook_id: str,
    worker_id: str,
    job_type: Optional[str] = None,
) -> Optional[dict]:
    """Pull next available job from the server."""
    params = {"worker_id": worker_id}
    if job_type:
        params["type"] = job_type

    resp = session.get(
        f"{server}/notebooks/{notebook_id}/jobs/next",
        params=params,
    )

    if resp.status_code == 200:
        data = resp.json()
        if data is None or data == {}:
            return None
        return data
    elif resp.status_code == 204:
        return None
    else:
        logger.warning("Failed to pull job: %s %s", resp.status_code, resp.text)
        return None


def complete_job(
    session: requests.Session,
    server: str,
    notebook_id: str,
    job_id: str,
    worker_id: str,
    result: dict,
) -> bool:
    """Submit completed job result to the server."""
    resp = session.post(
        f"{server}/notebooks/{notebook_id}/jobs/{job_id}/complete",
        json={"worker_id": worker_id, "result": result},
    )
    if resp.status_code == 200:
        return True
    else:
        logger.error("Failed to complete job %s: %s %s", job_id, resp.status_code, resp.text)
        return False


def fail_job(
    session: requests.Session,
    server: str,
    notebook_id: str,
    job_id: str,
    worker_id: str,
    error: str,
) -> bool:
    """Report job failure to the server."""
    resp = session.post(
        f"{server}/notebooks/{notebook_id}/jobs/{job_id}/fail",
        json={"worker_id": worker_id, "error": error},
    )
    return resp.status_code == 200


def process_job(client: anthropic.Anthropic, model: str, job: dict) -> dict:
    """Process a job by calling the LLM and parsing the response."""
    job_type = job["job_type"]
    payload = job["payload"]

    if job_type == "DISTILL_CLAIMS":
        prompt = build_distill_prompt(payload)
        response = call_llm(client, model, prompt)
        return parse_distill_result(response)

    elif job_type == "COMPARE_CLAIMS":
        prompt = build_compare_prompt(payload)
        response = call_llm(client, model, prompt)
        return parse_compare_result(response, payload)

    elif job_type == "CLASSIFY_TOPIC":
        prompt = build_classify_prompt(payload)
        response = call_llm(client, model, prompt)
        return parse_classify_result(response)

    else:
        raise ValueError(f"Unknown job type: {job_type}")


def call_llm(client: anthropic.Anthropic, model: str, prompt: str) -> str:
    """Call the LLM and return the text response."""
    message = client.messages.create(
        model=model,
        max_tokens=2048,
        messages=[{"role": "user", "content": prompt}],
    )
    return message.content[0].text


def run_worker(args: argparse.Namespace):
    """Main worker loop."""
    client = anthropic.Anthropic()
    session = requests.Session()
    session.headers["Authorization"] = f"Bearer {args.token}"
    session.headers["Content-Type"] = "application/json"

    logger.info(
        "Starting robot worker: id=%s model=%s server=%s notebook=%s",
        args.worker_id, args.model, args.server, args.notebook,
    )

    jobs_completed = 0
    jobs_failed = 0
    consecutive_empty = 0

    while True:
        try:
            job = pull_job(
                session, args.server, args.notebook,
                args.worker_id, args.job_type,
            )

            if job is None:
                consecutive_empty += 1
                if consecutive_empty % 12 == 1:  # Log every minute at 5s interval
                    logger.debug("No jobs available, waiting...")
                time.sleep(args.poll_interval)
                continue

            consecutive_empty = 0
            job_id = job["id"]
            job_type = job["job_type"]
            logger.info("Processing job %s (type=%s)", job_id, job_type)

            try:
                result = process_job(client, args.model, job)
                if complete_job(
                    session, args.server, args.notebook,
                    job_id, args.worker_id, result,
                ):
                    jobs_completed += 1
                    logger.info(
                        "Job %s completed (total: %d completed, %d failed)",
                        job_id, jobs_completed, jobs_failed,
                    )
                else:
                    jobs_failed += 1

            except Exception as e:
                logger.error("Job %s failed: %s", job_id, e)
                fail_job(
                    session, args.server, args.notebook,
                    job_id, args.worker_id, str(e),
                )
                jobs_failed += 1

        except KeyboardInterrupt:
            logger.info(
                "Shutting down. Completed: %d, Failed: %d",
                jobs_completed, jobs_failed,
            )
            break
        except Exception as e:
            logger.error("Unexpected error: %s", e)
            time.sleep(args.poll_interval)


def main():
    parser = argparse.ArgumentParser(description="Notebook robot worker")
    parser.add_argument("--server", required=True, help="Notebook server URL")
    parser.add_argument("--notebook", required=True, help="Notebook UUID")
    parser.add_argument("--worker-id", required=True, help="Worker identifier")
    parser.add_argument("--token", required=True, help="JWT Bearer token")
    parser.add_argument(
        "--job-type", default=None,
        choices=["DISTILL_CLAIMS", "COMPARE_CLAIMS", "CLASSIFY_TOPIC"],
        help="Only process this job type (default: all)",
    )
    parser.add_argument(
        "--model", default="claude-haiku-4-5-20251001",
        help="Anthropic model to use",
    )
    parser.add_argument(
        "--poll-interval", type=float, default=5.0,
        help="Seconds between poll attempts when no jobs available",
    )
    args = parser.parse_args()
    run_worker(args)


if __name__ == "__main__":
    main()
