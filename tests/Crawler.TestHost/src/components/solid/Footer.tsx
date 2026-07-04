import { For } from 'solid-js';
import { pages } from '../../shared/pages';

export default function Footer() {
    const columns = [pages.slice(0, 6), pages.slice(6, 12), pages.slice(12, 18)];
    return (
        <footer class="footer">
            <div class="footer__columns">
                <For each={columns}>
                    {(column, index) => (
                        <nav class="footer__column" aria-label={`Footer ${index() + 1}`}>
                            <ul>
                                <For each={column}>
                                    {(entry) => (
                                        <li>
                                            <a href={entry.href}>{entry.name}</a>
                                        </li>
                                    )}
                                </For>
                            </ul>
                        </nav>
                    )}
                </For>
            </div>
            <p class="footer__legal">© {new Date().getFullYear()} Fjällström Outfitters — test fixture.</p>
        </footer>
    );
}
