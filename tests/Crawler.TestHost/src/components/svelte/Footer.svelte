<script lang="ts">
import { pages } from '../../shared/pages';

const columns = [pages.slice(0, 6), pages.slice(6, 12), pages.slice(12, 18)];
const year = new Date().getFullYear();
</script>

<footer class="footer">
    <div class="footer__columns">
        {#each columns as column, index (index)}
            <nav class="footer__column" aria-label={`Footer ${index + 1}`}>
                <ul>
                    {#each column as entry (entry.href)}
                        <li><a href={entry.href}>{entry.name}</a></li>
                    {/each}
                </ul>
            </nav>
        {/each}
    </div>
    <p class="footer__legal">© {year} Fjällström Outfitters — test fixture.</p>
</footer>
