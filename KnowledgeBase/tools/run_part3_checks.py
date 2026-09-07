"""Current, bounded Part 3 verification using the existing checked child boundary."""
import argparse
import datetime
import hashlib
import json
import time
from pathlib import Path
from run_part1_checks import execute, validate_child_arguments

KB = Path(__file__).resolve().parents[1]
RESULT = KB / 'r02_part3_results.json'

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--stage', required=True)
    parser.add_argument('--configuration', choices=['Debug', 'Release'], default='Release')
    parser.add_argument('--reference', action='store_true')
    parser.add_argument('--timeout', type=int, default=120)
    parser.add_argument('arguments', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.arguments[1:] if args.arguments[:1] == ['--'] else args.arguments
    if not 1 <= args.timeout <= 120 or (args.reference and command):
        parser.error('Choose one command or reference batch, with timeout 1..120')
    jobs = [(f'reference-{seed}-{observe}', 60,
             ['--diagnostic-scenario', str(seed), '128', 'reference', observe, '1',
              'render' if observe == 'on' else 'no-render'])
            for seed in (11, 29) for observe in ('off', 'on')] if args.reference else [
                (args.stage, args.timeout, command or ['--all'])]
    for _, _, command in jobs:
        validate_child_arguments(command)
    data = json.loads(RESULT.read_text(encoding='utf-8')) if RESULT.exists() else {
        'actor': 'Seed Analyzer', 'part': 'R02 Part3',
        'contract_sha256': hashlib.sha256((KB/'R02_PART3_CONTRACT.md').read_bytes()).hexdigest().upper(),
        'checks': {}}
    if args.stage in data['checks']:
        parser.error('Stage already exists; use a distinct intervention stage rather than erasing an earlier result')
    budget = 300 if args.reference else args.timeout
    batch = {'configuration': args.configuration, 'budget_seconds': budget, 'runs': [], 'unstarted': []}
    started = time.monotonic()
    for index, (name, timeout, command) in enumerate(jobs):
        remaining = budget - (time.monotonic() - started)
        if remaining < 1:
            batch['unstarted'] = [job[0] for job in jobs[index:]]
            break
        row = execute(command, args.configuration, min(timeout, max(1, int(remaining))))
        row['name'] = name
        batch['runs'].append(row)
        data['checks'][args.stage] = batch
        RESULT.write_text(json.dumps(data, indent=2)+'\n', encoding='utf-8')
        last = row['records'][-1] if row['records'] else {}
        print(json.dumps({'name':name, 'exit_code':row['exit_code'], 'seconds':row['wall_seconds'],
              'result':{key:last[key] for key in ('total','passed','failed') if key in last},
              'messages':row['messages'][:8], 'stderr':row['stderr'][:1500]}), flush=True)
    batch['wall_seconds'] = round(time.monotonic()-started, 6)
    batch['ok'] = not batch['unstarted'] and all(row['exit_code']==0 and not row.get('incomplete') for row in batch['runs'])
    data['checks'][args.stage] = batch
    data['updated_at_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    RESULT.write_text(json.dumps(data, indent=2)+'\n', encoding='utf-8')
    return 0 if batch['ok'] else 1

if __name__ == '__main__':
    raise SystemExit(main())
