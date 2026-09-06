"""Bounded Part 2 checks; reuse the tested process boundary without touching Part 1 results."""
import argparse
import datetime
import hashlib
import json
import time
from pathlib import Path
from run_part1_checks import execute, validate_child_arguments

KB = Path(__file__).resolve().parents[1]
RESULT = KB / 'r02_part2_results.json'

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--stage', required=True)
    parser.add_argument('--configuration', choices=['Debug', 'Release'], default='Release')
    parser.add_argument('--batch', choices=['reference', 'production'])
    parser.add_argument('--timeout', type=int, default=120)
    parser.add_argument('arguments', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    arguments = args.arguments[1:] if args.arguments[:1] == ['--'] else args.arguments
    if not 1 <= args.timeout <= 120 or (args.batch and arguments):
        parser.error('Use one command or batch and a timeout of 1..120 seconds')
    if args.batch == 'reference':
        budget = 300
        jobs = [(f'reference-{seed}-{observe}', 60,
            ['--diagnostic-scenario', str(seed), '128', 'reference', observe, '1', 'render' if observe == 'on' else 'no-render'])
            for seed in (11, 29) for observe in ('off', 'on')]
    elif args.batch == 'production':
        budget = 600
        jobs = [(f'production-{seed}', 120,
            ['--diagnostic-scenario', str(seed), '2048', 'production', 'on', '8', 'no-render'])
            for seed in (11, 29, 47, 83)]
    else:
        budget = args.timeout
        jobs = [(args.stage, args.timeout, arguments or ['--all'])]
    for _, _, command in jobs: validate_child_arguments(command)
    contract = KB / 'R02_PART2_CONTRACT.md'
    data = json.loads(RESULT.read_text()) if RESULT.exists() else {
        'actor': 'Seed Analyzer', 'part': 'R02 Part2', 'status': 'verification in progress',
        'contract_sha256': hashlib.sha256(contract.read_bytes()).hexdigest().upper(), 'checks': {}}
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
        RESULT.write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')
        print(json.dumps({'name': name, 'exit_code': row['exit_code'], 'wall_seconds': row['wall_seconds'],
            'record_count': len(row['records']), 'messages': row['messages'][:8], 'stderr': row['stderr'][:1000]}), flush=True)
    batch['wall_seconds'] = round(time.monotonic() - started, 3)
    batch['ok'] = not batch['unstarted'] and all(row['exit_code'] == 0 and not row.get('incomplete') for row in batch['runs'])
    data['checks'][args.stage] = batch
    data['updated_at_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    RESULT.write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')
    return 0 if batch['ok'] else 1

if __name__ == '__main__':
    raise SystemExit(main())
